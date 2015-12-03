namespace NAppUpdate.Framework.Common
{
    public interface IRestartExternApp
    {
        bool Start();
        bool Stop();
        bool Restart();
    }
}