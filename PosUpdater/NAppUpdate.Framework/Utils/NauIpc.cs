using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using NAppUpdate.Framework.Common;
using NAppUpdate.Framework.Tasks;

namespace NAppUpdate.Framework.Utils
{
	/// <summary>
	/// Starts the cold update process by extracting the updater app from the library's resources,
	/// passing it all the data it needs and terminating the current application
	/// </summary>
	internal static class NauIpc
	{
        private static string _assemblyPathEndsWith = "AppUpdate.Framework.dll";
        
        [Serializable]
		internal class NauDto
		{
			public NauConfigurations Configs { get; set; }
			public IList<IUpdateTask> Tasks { get; set; }
			public List<Logger.LogItem> LogItems { get; set; }
			public string AppPath { get; set; }
			public string WorkingDirectory { get; set; }
			public bool RelaunchApplication { get; set; }
		}
        
        [DllImport("kernel32.dll")]
        public static extern uint GetLastError();
		
        [DllImport("kernel32.dll", SetLastError = true)]
		private static extern SafeFileHandle CreateNamedPipe(
		   String pipeName,
		   uint dwOpenMode,
		   uint dwPipeMode,
		   uint nMaxInstances,
		   uint nOutBufferSize,
		   uint nInBufferSize,
		   uint nDefaultTimeOut,
		   SECURITY_ATTRIBUTES lpSecurityAttributes);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern int ConnectNamedPipe(
		   SafeFileHandle hNamedPipe,
		   IntPtr lpOverlapped);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern SafeFileHandle CreateFile(
		   String pipeName,
		   uint dwDesiredAccess,
		   uint dwShareMode,
		   IntPtr lpSecurityAttributes,
		   uint dwCreationDisposition,
		   uint dwFlagsAndAttributes,
		   IntPtr hTemplate);

		//private const uint DUPLEX = (0x00000003);
		private const uint WRITE_ONLY = (0x00000002);
		private const uint FILE_FLAG_OVERLAPPED = (0x40000000);
	    private const uint ERROR_PIPE_CONNECTED = 535;

		const uint GENERIC_READ = (0x80000000);
		//static readonly uint GENERIC_WRITE = (0x40000000);
		const uint OPEN_EXISTING = 3;

		internal static string GetPipeName(string syncProcessName)
		{
			return string.Format("\\\\.\\pipe\\{0}", syncProcessName);
		}

		private class State
		{
			public readonly EventWaitHandle eventWaitHandle;
			public int result { get; set; }
			public SafeFileHandle clientPipeHandle { get; set; }

			public State()
			{
				eventWaitHandle = new ManualResetEvent(false);
			}
		}

		internal static uint BUFFER_SIZE = 4096;

		public static Process LaunchProcessAndSendDto(NauDto dto, ProcessStartInfo processStartInfo, string syncProcessName)
		{
			Process p;
			State state = new State();
            SECURITY_ATTRIBUTES sa = null;
            sa = CreateNativePipeSecurity();
            using (state.clientPipeHandle = CreateNamedPipe(
				   GetPipeName(syncProcessName),
				   WRITE_ONLY | FILE_FLAG_OVERLAPPED,
				   0,
				   1, // 1 max instance (only the updater utility is expected to connect)
				   BUFFER_SIZE,
				   BUFFER_SIZE,
				   0,
				   sa))
			{
				//failed to create named pipe
				if (state.clientPipeHandle.IsInvalid) return null;

				try
				{
					p = Process.Start(processStartInfo);
				}
				catch (Win32Exception)
				{
					// Person denied UAC escalation
					return null;
				}

				ThreadPool.QueueUserWorkItem(ConnectPipe, state);
				//A rather arbitrary five seconds, perhaps better to be user configurable at some point?
				state.eventWaitHandle.WaitOne(60000);

				//failed to connect client pipe
				if (state.result == 0) return null;
				//client connection successfull
				using (var fStream = new FileStream(state.clientPipeHandle, FileAccess.Write, (int)BUFFER_SIZE, true))
				{
					new BinaryFormatter().Serialize(fStream, dto);
					fStream.Flush();
					fStream.Close();
				}
			}

			return p;
		}

        /// <summary>
        /// The SECURITY_ATTRIBUTES structure contains the security descriptor for 
        /// an object and specifies whether the handle retrieved by specifying 
        /// this structure is inheritable. This structure provides security 
        /// settings for objects created by various functions, such as CreateFile, 
        /// CreateNamedPipe, CreateProcess, RegCreateKeyEx, or RegSaveKeyEx.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal class SECURITY_ATTRIBUTES
        {
            public int nLength;
            public SafeLocalMemHandle lpSecurityDescriptor;
            public bool bInheritHandle;
        }

        /// <summary>
        /// Represents a wrapper class for a local memory pointer. 
        /// </summary>
        [SuppressUnmanagedCodeSecurity,
        HostProtection(SecurityAction.LinkDemand, MayLeakOnAbort = true)]
        internal sealed class SafeLocalMemHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public SafeLocalMemHandle()
                : base(true)
            {
            }

            public SafeLocalMemHandle(IntPtr preexistingHandle, bool ownsHandle)
                : base(ownsHandle)
            {
                base.SetHandle(preexistingHandle);
            }

            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success),
            DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            private static extern IntPtr LocalFree(IntPtr hMem);

            protected override bool ReleaseHandle()
            {
                return (LocalFree(base.handle) == IntPtr.Zero);
            }
        }

        /// <summary>
        /// The CreateNativePipeSecurity function creates and initializes a new 
        /// SECURITY_ATTRIBUTES object to allow Authenticated Users read and 
        /// write access to a pipe, and to allow the Administrators group full 
        /// access to the pipe.
        /// </summary>
        /// <returns>
        /// A SECURITY_ATTRIBUTES object that allows Authenticated Users read and 
        /// write access to a pipe, and allows the Administrators group full 
        /// access to the pipe.
        /// </returns>
        /// <see cref="http://msdn.microsoft.com/en-us/library/aa365600(VS.85).aspx"/>
        static SECURITY_ATTRIBUTES CreateNativePipeSecurity()
        {
            // Define the SDDL for the security descriptor.
            string sddl = "D:" +        // Discretionary ACL
                "(A;OICI;GRGW;;;AU)" +  // Allow read/write to authenticated users
                "(A;OICI;GA;;;BA)";     // Allow full control to administrators

            SafeLocalMemHandle pSecurityDescriptor = null;
            if (!NativeMethod.ConvertStringSecurityDescriptorToSecurityDescriptor(
                sddl, 1, out pSecurityDescriptor, IntPtr.Zero))
            {
                throw new Win32Exception();
            }

            SECURITY_ATTRIBUTES sa = new SECURITY_ATTRIBUTES();
            sa.nLength = Marshal.SizeOf(sa);
            sa.lpSecurityDescriptor = pSecurityDescriptor;
            sa.bInheritHandle = false;
            return sa;
        }

        /// <summary>
        /// Desired Access of File/Device
        /// </summary>
        [Flags]
        internal enum FileDesiredAccess : uint
        {
            GENERIC_READ = 0x80000000,
            GENERIC_WRITE = 0x40000000,
            GENERIC_EXECUTE = 0x20000000,
            GENERIC_ALL = 0x10000000
        }

        /// <summary>
        /// File share mode
        /// </summary>
        [Flags]
        internal enum FileShareMode : uint
        {
            Zero = 0x00000000,                  // No sharing.
            FILE_SHARE_DELETE = 0x00000004,
            FILE_SHARE_READ = 0x00000001,
            FILE_SHARE_WRITE = 0x00000002
        }

        /// <summary>
        /// File Creation Disposition
        /// </summary>
        internal enum FileCreationDisposition : uint
        {
            CREATE_NEW = 1,
            CREATE_ALWAYS = 2,
            OPEN_EXISTING = 3,
            OPEN_ALWAYS = 4,
            TRUNCATE_EXISTING = 5
        }

        /// <summary>
        /// Named Pipe Open Modes
        /// http://msdn.microsoft.com/en-us/library/aa365596.aspx
        /// </summary>
        [Flags]
        internal enum PipeOpenMode : uint
        {
            PIPE_ACCESS_INBOUND = 0x00000001,   // Inbound pipe access.
            PIPE_ACCESS_OUTBOUND = 0x00000002,  // Outbound pipe access.
            PIPE_ACCESS_DUPLEX = 0x00000003     // Duplex pipe access.
        }

        /// <summary>
        /// Named Pipe Type, Read, and Wait Modes
        /// http://msdn.microsoft.com/en-us/library/aa365605.aspx
        /// </summary>
        internal enum PipeMode : uint
        {
            // Type Mode
            PIPE_TYPE_BYTE = 0x00000000,        // Byte pipe type.
            PIPE_TYPE_MESSAGE = 0x00000004,     // Message pipe type.

            // Read Mode
            PIPE_READMODE_BYTE = 0x00000000,    // Read mode of type Byte.
            PIPE_READMODE_MESSAGE = 0x00000002, // Read mode of type Message.

            // Wait Mode
            PIPE_WAIT = 0x00000000,             // Pipe blocking mode.
            PIPE_NOWAIT = 0x00000001            // Pipe non-blocking mode.
        }

        /// <summary>
        /// The class exposes Windows APIs to be used in this code sample.
        /// </summary>
        [SuppressUnmanagedCodeSecurity]
        internal class NativeMethod
        {
            /// <summary>
            /// Creates an instance of a named pipe and returns a handle for 
            /// subsequent pipe operations.
            /// </summary>
            /// <param name="pipeName">Pipe name</param>
            /// <param name="openMode">Pipe open mode</param>
            /// <param name="pipeMode">Pipe-specific modes</param>
            /// <param name="maxInstances">Maximum number of instances</param>
            /// <param name="outBufferSize">Output buffer size</param>
            /// <param name="inBufferSize">Input buffer size</param>
            /// <param name="defaultTimeout">Time-out interval</param>
            /// <param name="securityAttributes">Security attributes</param>
            /// <returns>If the function succeeds, the return value is a handle 
            /// to the server end of a named pipe instance.</returns>
            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern SafePipeHandle CreateNamedPipe(string pipeName,
                PipeOpenMode openMode, PipeMode pipeMode, int maxInstances,
                int outBufferSize, int inBufferSize, uint defaultTimeout,
                SECURITY_ATTRIBUTES securityAttributes);


            /// <summary>
            /// Enables a named pipe server process to wait for a client process to 
            /// connect to an instance of a named pipe.
            /// </summary>
            /// <param name="hNamedPipe">
            /// Handle to the server end of a named pipe instance.
            /// </param>
            /// <param name="overlapped">Pointer to an Overlapped object.</param>
            /// <returns>
            /// If the function succeeds, the return value is true.
            /// </returns>
            [return: MarshalAs(UnmanagedType.Bool)]
            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern bool ConnectNamedPipe(SafePipeHandle hNamedPipe,
                IntPtr overlapped);


            /// <summary>
            /// Waits until either a time-out interval elapses or an instance of the 
            /// specified named pipe is available for connection (that is, the pipe's 
            /// server process has a pending ConnectNamedPipe operation on the pipe).
            /// </summary>
            /// <param name="pipeName">The name of the named pipe.</param>
            /// <param name="timeout">
            /// The number of milliseconds that the function will wait for an 
            /// instance of the named pipe to be available.
            /// </param>
            /// <returns>
            /// If an instance of the pipe is available before the time-out interval 
            /// elapses, the return value is true.
            /// </param>
            [return: MarshalAs(UnmanagedType.Bool)]
            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern bool WaitNamedPipe(string pipeName, uint timeout);


            /// <summary>
            /// Sets the read mode and the blocking mode of the specified named pipe.
            /// </summary>
            /// <remarks>
            /// If the specified handle is to the client end of a named pipe and if
            /// the named pipe server process is on a remote computer, the function
            /// can also be used to control local buffering.
            /// </remarks>
            /// <param name="hNamedPipe">Handle to the named pipe instance.</param>
            /// <param name="mode">
            /// Pointer to a variable that supplies the new mode.
            /// </param>
            /// <param name="maxCollectionCount">
            /// Reference to a variable that specifies the maximum number of bytes 
            /// collected on the client computer before transmission to the server.
            /// </param>
            /// <param name="collectDataTimeout">
            /// Reference to a variable that specifies the maximum time, in 
            /// milliseconds, that can pass before a remote named pipe transfers 
            /// information over the network.
            /// </param>
            /// <returns>If the function succeeds, the return value is true.</returns>
            [return: MarshalAs(UnmanagedType.Bool)]
            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern bool SetNamedPipeHandleState(
                SafePipeHandle hNamedPipe, ref PipeMode mode,
                IntPtr maxCollectionCount, IntPtr collectDataTimeout);


            /// <summary>
            /// Creates or opens a file, directory, physical disk, volume, console 
            /// buffer, tape drive, communications resource, mailslot, or named pipe.
            /// </summary>
            /// <param name="fileName">
            /// The name of the file or device to be created or opened.
            /// </param>
            /// <param name="desiredAccess">
            /// The requested access to the file or device, which can be summarized 
            /// as read, write, both or neither (zero).
            /// </param>
            /// <param name="shareMode">
            /// The requested sharing mode of the file or device, which can be read, 
            /// write, both, delete, all of these, or none (refer to the following 
            /// table). 
            /// </param>
            /// <param name="securityAttributes">
            /// A SECURITY_ATTRIBUTES object that contains two separate but related 
            /// data members: an optional security descriptor, and a Boolean value 
            /// that determines whether the returned handle can be inherited by 
            /// child processes.
            /// </param>
            /// <param name="creationDisposition">
            /// An action to take on a file or device that exists or does not exist.
            /// </param>
            /// <param name="flagsAndAttributes">
            /// The file or device attributes and flags.
            /// </param>
            /// <param name="hTemplateFile">Handle to a template file.</param>
            /// <returns>
            /// If the function succeeds, the return value is an open handle to the 
            /// specified file, device, named pipe, or mail slot.
            /// If the function fails, the return value is INVALID_HANDLE_VALUE.
            /// </returns>
            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern SafePipeHandle CreateFile(string fileName,
                FileDesiredAccess desiredAccess, FileShareMode shareMode,
                SECURITY_ATTRIBUTES securityAttributes,
                FileCreationDisposition creationDisposition,
                int flagsAndAttributes, IntPtr hTemplateFile);


            /// <summary>
            /// Reads data from the specified file or input/output (I/O) device.
            /// </summary>
            /// <param name="handle">
            /// A handle to the device (for example, a file, file stream, physical 
            /// disk, volume, console buffer, tape drive, socket, communications 
            /// resource, mailslot, or pipe).
            /// </param>
            /// <param name="bytes">
            /// A buffer that receives the data read from a file or device.
            /// </param>
            /// <param name="numBytesToRead">
            /// The maximum number of bytes to be read.
            /// </param>
            /// <param name="numBytesRead">
            /// The number of bytes read when using a synchronous IO.
            /// </param>
            /// <param name="overlapped">
            /// A pointer to an OVERLAPPED structure if the file was opened with 
            /// FILE_FLAG_OVERLAPPED.
            /// </param> 
            /// <returns>
            /// If the function succeeds, the return value is true. If the function 
            /// fails, or is completing asynchronously, the return value is false.
            /// </returns>
            [return: MarshalAs(UnmanagedType.Bool)]
            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern bool ReadFile(SafePipeHandle handle, byte[] bytes,
                int numBytesToRead, out int numBytesRead, IntPtr overlapped);


            /// <summary>
            /// Writes data to the specified file or input/output (I/O) device.
            /// </summary>
            /// <param name="handle">
            /// A handle to the file or I/O device (for example, a file, file stream,
            /// physical disk, volume, console buffer, tape drive, socket, 
            /// communications resource, mailslot, or pipe). 
            /// </param>
            /// <param name="bytes">
            /// A buffer containing the data to be written to the file or device.
            /// </param>
            /// <param name="numBytesToWrite">
            /// The number of bytes to be written to the file or device.
            /// </param>
            /// <param name="numBytesWritten">
            /// The number of bytes written when using a synchronous IO.
            /// </param>
            /// <param name="overlapped">
            /// A pointer to an OVERLAPPED structure is required if the file was 
            /// opened with FILE_FLAG_OVERLAPPED.
            /// </param>
            /// <returns>
            /// If the function succeeds, the return value is true. If the function 
            /// fails, or is completing asynchronously, the return value is false.
            /// </returns>
            [return: MarshalAs(UnmanagedType.Bool)]
            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern bool WriteFile(SafePipeHandle handle, byte[] bytes,
                int numBytesToWrite, out int numBytesWritten, IntPtr overlapped);


            /// <summary>
            /// Flushes the buffers of the specified file and causes all buffered 
            /// data to be written to the file.
            /// </summary>
            /// <param name="hHandle">A handle to the open file. </param>
            /// <returns>
            /// If the function succeeds, the return value is true.
            /// </returns>
            [return: MarshalAs(UnmanagedType.Bool)]
            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern bool FlushFileBuffers(SafePipeHandle handle);


            /// <summary>
            /// Disconnects the server end of a named pipe instance from a client
            /// process.
            /// </summary>
            /// <param name="hNamedPipe">Handle to a named pipe instance.</param>
            /// <returns>
            /// If the function succeeds, the return value is true.
            /// </returns>
            [return: MarshalAs(UnmanagedType.Bool)]
            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern bool DisconnectNamedPipe(SafePipeHandle hNamedPipe);


            /// <summary>
            /// The ConvertStringSecurityDescriptorToSecurityDescriptor function 
            /// converts a string-format security descriptor into a valid, 
            /// functional security descriptor.
            /// </summary>
            /// <param name="sddlSecurityDescriptor">
            /// A string containing the string-format security descriptor (SDDL) 
            /// to convert.
            /// </param>
            /// <param name="sddlRevision">
            /// The revision level of the sddlSecurityDescriptor string. 
            /// Currently this value must be 1.
            /// </param>
            /// <param name="pSecurityDescriptor">
            /// A pointer to a variable that receives a pointer to the converted 
            /// security descriptor.
            /// </param>
            /// <param name="securityDescriptorSize">
            /// A pointer to a variable that receives the size, in bytes, of the 
            /// converted security descriptor. This parameter can be IntPtr.Zero.
            /// </param>
            /// <returns>
            /// If the function succeeds, the return value is true.
            /// </returns>
            [return: MarshalAs(UnmanagedType.Bool)]
            [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(
                string sddlSecurityDescriptor, int sddlRevision,
                out SafeLocalMemHandle pSecurityDescriptor,
                IntPtr securityDescriptorSize);
        }


		internal static void ConnectPipe(object stateObject)
		{
			if (stateObject == null) return;
			State state = (State)stateObject;

		    try
		    {
		        state.result = ConnectNamedPipe(state.clientPipeHandle, IntPtr.Zero);
		        var err = GetLastError();
		        state.result = (err == ERROR_PIPE_CONNECTED) ? 0 : 1;
		    }
		    catch
		    {
		        state.result = -1;
		    }
			state.eventWaitHandle.Set(); // signal we're done
		}


		internal static object ReadDto(string syncProcessName)
		{
			using (SafeFileHandle pipeHandle = CreateFile(
				GetPipeName(syncProcessName),
				GENERIC_READ,
				0,
				IntPtr.Zero,
				OPEN_EXISTING,
				FILE_FLAG_OVERLAPPED,
				IntPtr.Zero))
			{

				if (pipeHandle.IsInvalid)
					return null;

			    using (var fStream = new FileStream(pipeHandle, FileAccess.Read, (int) BUFFER_SIZE, true))
			    {
			        return new BinaryFormatter().Deserialize(fStream);
			    }
			}
		}

		internal static void ExtractUpdaterFromResource(string updaterPath, string hostExeName)
		{
			if (!Directory.Exists(updaterPath))
				Directory.CreateDirectory(updaterPath);

			//store the updater temporarily in the designated folder            
			using (var writer = new BinaryWriter(File.Open(Path.Combine(updaterPath, hostExeName), FileMode.Create)))
				writer.Write(Resources.updater);

			// Now copy the NAU DLL
			var assemblyLocation = typeof(NauIpc).Assembly.Location;
            File.Copy(assemblyLocation, Path.Combine(updaterPath, _assemblyPathEndsWith), true);

			// And also all other referenced DLLs (opt-in only)
			var assemblyPath = Path.GetDirectoryName(assemblyLocation) ?? string.Empty;
			if (UpdateManager.Instance.Config.DependenciesForColdUpdate == null) return;
			// TODO Maybe we can back this up with typeof(UpdateStarter).Assembly.GetReferencedAssemblies()

			foreach (var dep in UpdateManager.Instance.Config.DependenciesForColdUpdate)
			{
				string fullPath = Path.Combine(assemblyPath, dep);
				if (!File.Exists(fullPath)) continue;

				var dest = Path.Combine(updaterPath, dep);
				FileSystem.CreateDirectoryStructure(dest);
				File.Copy(fullPath, Path.Combine(updaterPath, dep), true);
			}
		}
	}
}