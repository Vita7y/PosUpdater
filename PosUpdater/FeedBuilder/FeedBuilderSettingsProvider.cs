using System;
using System.Collections;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using FeedBuilder.Properties;

namespace FeedBuilder
{
    public class FeedBuilderSettingsProvider : SettingsProvider
    {
        //XML Root Node
        private const string SettingsRoot = "Settings";

        public void SaveAs(string filename)
        {
            try
            {
                Settings.Default.Save();
                string source = Path.Combine(GetAppSettingsPath(), GetAppSettingsFilename());
                File.Copy(source, filename, true);
            }
            catch (Exception ex)
            {
                string msg = string.Format("An error occurred while saving the file: {0}{0}{1}", Environment.NewLine, ex.Message);
                MessageBox.Show(msg, @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void Load()
        {
            try
            {
                _settingsXml = null;
                Settings.Default.Reload();
            }
            catch (Exception ex)
            {
                string msg = string.Format("An error occurred while loading the file: {0}{0}{1}", Environment.NewLine, ex.Message);
                MessageBox.Show(msg, @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void LoadFrom(string filename)
        {
            try
            {
                string dest = Path.Combine(GetAppSettingsPath(), GetAppSettingsFilename());
                if (filename == dest) return;
                File.Copy(filename, dest, true);
                Load();
            }
            catch (Exception ex)
            {
                string msg = string.Format("An error occurred while loading the file: {0}{0}{1}", Environment.NewLine, ex.Message);
                MessageBox.Show(msg, @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public override void Initialize(string name, NameValueCollection col)
        {
            base.Initialize(ApplicationName, col);
            if (!Directory.Exists(GetAppSettingsPath()))
            {
                try
                {
                    Directory.CreateDirectory(GetAppSettingsPath());
                }
                catch (IOException)
                {
                }
            }
        }

        public override string ApplicationName
        {
            get { return "FeedBuilder"; }
            //Do nothing
            set { }
        }

        public virtual string GetAppSettingsPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ApplicationName);
        }

        public virtual string GetAppSettingsFilename()
        {
            return "Settings.xml";
        }

        public override void SetPropertyValues(SettingsContext context, SettingsPropertyValueCollection propvals)
        {
            //Iterate through the settings to be stored
            //Only dirty settings are included in propvals, and only ones relevant to this provider
            foreach (SettingsPropertyValue propval in propvals)
            {
                SetValue(propval);
            }

            try
            {
                if (!Directory.Exists(GetAppSettingsPath())) Directory.CreateDirectory(GetAppSettingsPath());
                SettingsXml.Save(Path.Combine(GetAppSettingsPath(), GetAppSettingsFilename()));
            }
            catch
            {
                //Ignore if cant save, device been ejected
            }
        }

        public override SettingsPropertyValueCollection GetPropertyValues(SettingsContext context,
                                                                          SettingsPropertyCollection props)
        {
            //Create new collection of values
            SettingsPropertyValueCollection values = new SettingsPropertyValueCollection();

            //Iterate through the settings to be retrieved

            foreach (SettingsProperty setting in props)
            {
                SettingsPropertyValue value = new SettingsPropertyValue(setting)
                                                  {
                                                      IsDirty = false,
                                                      SerializedValue = GetValue(setting)
                                                  };
                values.Add(value);
            }
            return values;
        }

        private static XmlDocument _settingsXml;

        private XmlDocument SettingsXml
        {
            get
            {
                //If we dont hold an xml document, try opening one.  
                //If it doesnt exist then create a new one ready.
                if (_settingsXml == null)
                {
                    _settingsXml = new XmlDocument();

                    try
                    {
                        _settingsXml.Load(Path.Combine(GetAppSettingsPath(), GetAppSettingsFilename()));
                    }
                    catch (Exception)
                    {
                        //Create new document
                        XmlDeclaration dec = _settingsXml.CreateXmlDeclaration("1.0", "utf-8", string.Empty);
                        _settingsXml.AppendChild(dec);

                        XmlNode nodeRoot = _settingsXml.CreateNode(XmlNodeType.Element, SettingsRoot, "");
                        _settingsXml.AppendChild(nodeRoot);
                    }
                }

                return _settingsXml;
            }
        }

        private string GetValue(SettingsProperty setting)
        {
            string ret = null;

            try
            {
                string path = IsRoaming(setting)
                                  ? string.Format("{0}/{1}", SettingsRoot, setting.Name)
                                  : string.Format("{0}/{1}/{2}", SettingsRoot, Environment.MachineName, setting.Name);

                if (setting.PropertyType.BaseType != null
                    && setting.PropertyType.BaseType.Name == "CollectionBase")
                {
                    XmlNode selectSingleNode = SettingsXml.SelectSingleNode(path);
                    if (selectSingleNode != null) ret = selectSingleNode.InnerXml;
                }
                else
                {
                    XmlNode singleNode = SettingsXml.SelectSingleNode(path);
                    if (singleNode != null) ret = singleNode.InnerText;
                }
            }
            catch (Exception)
            {
                ret = (setting.DefaultValue != null) ? setting.DefaultValue.ToString() : string.Empty;
            }

            return ret;
        }

        private void SetValue(SettingsPropertyValue propVal)
        {
            XmlElement settingNode;

            //Determine if the setting is roaming.
            //If roaming then the value is stored as an element under the root
            //Otherwise it is stored under a machine name node 
            try
            {
                if (IsRoaming(propVal.Property))
                {
                    settingNode = (XmlElement) SettingsXml.SelectSingleNode(SettingsRoot + "/" + propVal.Name);
                }
                else
                {
                    settingNode = (XmlElement)SettingsXml.SelectSingleNode(SettingsRoot + "/" + Environment.MachineName + "/" + propVal.Name);
                }
            }
            catch (Exception)
            {
                settingNode = null;
            }

            //Check to see if the node exists, if so then set its new value
            if ((settingNode != null))
            {
                //SettingNode.InnerText = propVal.SerializedValue.ToString
                SetSerializedValue(settingNode, propVal);
            }
            else
            {
                if (IsRoaming(propVal.Property))
                {
                    //Store the value as an element of the Settings Root Node
                    settingNode = SettingsXml.CreateElement(propVal.Name);

                    //SettingNode.InnerText = propVal.SerializedValue.ToString
                    SetSerializedValue(settingNode, propVal);

                    XmlNode selectSingleNode = SettingsXml.SelectSingleNode(SettingsRoot);
                    if (selectSingleNode != null) selectSingleNode.AppendChild(settingNode);
                }
                else
                {
                    //Its machine specific, store as an element of the machine name node,
                    //creating a new machine name node if one doesnt exist.
                    XmlElement machineNode;
                    try
                    {
                        machineNode = (XmlElement) SettingsXml.SelectSingleNode(SettingsRoot + "/" + Environment.MachineName);
                    }
                    catch (Exception)
                    {
                        machineNode = SettingsXml.CreateElement(Environment.MachineName);
                        XmlNode selectSingleNode = SettingsXml.SelectSingleNode(SettingsRoot);
                        if (selectSingleNode != null) selectSingleNode.AppendChild(machineNode);
                    }

                    if (machineNode == null)
                    {
                        machineNode = SettingsXml.CreateElement(Environment.MachineName);
                        XmlNode selectSingleNode = SettingsXml.SelectSingleNode(SettingsRoot);
                        if (selectSingleNode != null) selectSingleNode.AppendChild(machineNode);
                    }

                    settingNode = SettingsXml.CreateElement(propVal.Name);
                    //SettingNode.InnerText = propVal.SerializedValue.ToString
                    SetSerializedValue(settingNode, propVal);
                    machineNode.AppendChild(settingNode);
                }
            }
        }

        private void SetSerializedValue(XmlElement node, SettingsPropertyValue propVal)
        {
            if (propVal.Property.PropertyType.BaseType != null
                && propVal.Property.PropertyType.BaseType.Name == "CollectionBase")
            {
                StringBuilder builder = new StringBuilder();
                XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
                XmlWriterSettings xsettings = new XmlWriterSettings();

                ns.Add("", "");
                xsettings.OmitXmlDeclaration = true;
                XmlWriter xmlWriter = XmlWriter.Create(builder, xsettings);
                XmlSerializer s = new XmlSerializer(propVal.Property.PropertyType);
                s.Serialize(xmlWriter, propVal.PropertyValue, ns);
                xmlWriter.Close();
                node.InnerXml = builder.ToString();
            }
            else
            {
                if (propVal.Property.SerializeAs == SettingsSerializeAs.String)
                {
                    node.InnerText = propVal.PropertyValue.ToString();
                }
                else if (propVal.Property.SerializeAs == SettingsSerializeAs.Xml)
                {
                    // Serialize collection into XML manually
                    XmlSerializer serializer = new XmlSerializer(Settings.Default[propVal.Name].GetType());

                    StringBuilder sb = new StringBuilder();
                    XmlWriter writer = XmlWriter.Create(sb);

                    serializer.Serialize(writer, Settings.Default[propVal.Name]);
                    writer.Close();

                    node.InnerText = sb.ToString();

                }
                //node.InnerText = propVal.SerializedValue.ToString();
            }
        }

        private bool IsRoaming(SettingsProperty prop)
        {
            //Determine if the setting is marked as Roaming
            foreach (DictionaryEntry d in prop.Attributes)
            {
                Attribute a = (Attribute) d.Value;
                if (a is SettingsManageabilityAttribute) return true;
            }
            return false;
        }

    }
}
