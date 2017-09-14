using System;
using System.Collections.Generic;
using System.Xml;
using System.Reflection;
using System.IO;
using System.Xml.Schema;
using System.Net;
using System.Windows.Forms;
using System.Configuration;

namespace NETUpdater.Update
{
    public class ConfigLoader
    {
        private const string SchemaTargetName = "ConfigSchema";
        private const string SchemaUri = "ConfigSchema.xsd";
        private List<Config> localConfigs;
        private XmlDocument document;
        private string _filename;
        private UpdaterSettings _settings;
        private const int BinaryReaderBufferSize = 4096;


        public ConfigLoader(string updatePath, UpdaterSettings settings)
        {
            _settings = settings;
            DirectoryInfo path = new DirectoryInfo(String.Format("{1}{0}NETUpdater{0}NETUpdater{0}LocalConfig{0}",
                    Path.DirectorySeparatorChar,
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)));
            if (!path.Exists) path.Create();
            _filename = String.Format("{1}{0}NETUpdater{0}NETUpdater{0}LocalConfig{0}{2}",
                    Path.DirectorySeparatorChar,
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    _settings.LocalConfigName);
            localConfigs = new List<Config>();
            document = new XmlDocument();
            string[] str = Assembly.GetExecutingAssembly().GetManifestResourceNames();
            Stream _schemaStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NETUpdater.Update.ConfigSchema.xsd");
            XmlReader _schemaReader = XmlReader.Create(_schemaStream);
            document.Schemas.Add(SchemaTargetName, _schemaReader);
            document.Schemas.Compile();
        }
        public void ReadLocal()
        {
            FileInfo fileinfo = new FileInfo(_filename);
            if (!fileinfo.Exists) return;
            try
            {
                document.Load(_filename);
                document.Validate(new ValidationEventHandler(document_ValidationEventHandler));
                XmlElement root = document.DocumentElement;
                XmlNamespaceManager nsmgr = new XmlNamespaceManager(document.NameTable);
                nsmgr.AddNamespace("xs", SchemaTargetName);
                foreach (XmlNode product in root.ChildNodes)
                {
                    var configItem = new Config();
                    configItem.Version = XmlConvert.ToInt32(product.Attributes["version"].Value);
                    configItem.File = product.Attributes["file"].Value;
                    configItem.ProductId = product.Attributes["name"].Value;
                    localConfigs.Add(configItem);
                }
            }
            catch (XmlException e)
            {
                throw new Exception("Ошибка чтения файла конфигурации.", e);
            }
        }
        public Config GetLocal()
        {
            return localConfigs.Find(x => x.ProductId == _settings.ProductId);
        }
        public void ResetLocalConfig()
        {
            SetLocal(new Config() { ProductId = _settings.ProductId, Version = _settings.DefaultLocalPackageVersion });
        }
        public void SetLocal(Config config)
        {
            Config localConfig = localConfigs.Find(x => x.ProductId == config.ProductId);
            if (localConfig != null)
            {
                localConfig.Version = config.Version;
            }
            else
                localConfigs.Add(config);
            Save();
        }
        public void Save()
        {
            XmlDocument document = new XmlDocument();
            XmlDeclaration declar = document.CreateXmlDeclaration("1.0", "utf-8", null);
            XmlElement root = document.CreateElement("configs");
            foreach (Config item in localConfigs)
            {
                XmlElement config = document.CreateElement("config");
                config.SetAttribute("name", item.ProductId);
                config.SetAttribute("version", item.Version.ToString());
                config.SetAttribute("file", item.File);
                root.AppendChild(config);
            }
            document.AppendChild(root);
            document.InsertBefore(declar, root);
            document.Save(_filename);
        }
        private string PrepareDownloadPath(string path)
        {
            if (path.IndexOf("http://") == 0 || path.IndexOf("https://") == 0)
            {
                return String.Format("{0}?rnd={1}", path, Guid.NewGuid().ToString());
            }
            return path;
        }
        public string DownloadUpdate(Config config)
        {
            var remoteUri = new Uri(PrepareDownloadPath(String.Format(@"{0}/{1}", _settings.RemoteConfigPath, config.File)));
            string dirPath = String.Format(@"{0}/patches", Application.StartupPath);
            System.IO.Directory.CreateDirectory(dirPath);
            try
            {
                System.IO.DirectoryInfo di = new DirectoryInfo(dirPath);

                foreach (FileInfo file in di.GetFiles())
                {
                    file.Delete();
                }
            }
            catch (Exception) { }
            string localPath = PrepareDownloadPath(String.Format(@"{0}/{1}", dirPath, config.File));
            using (var client = new WebClient())
            {
                if (!AppSettings.AllowProxy)
                {
                    client.Proxy = null;
                }
                client.UseDefaultCredentials = true;
                using (var remoteStream = client.OpenRead(remoteUri))
                {
                    using (var fileStream = new FileStream(localPath, FileMode.Create))
                    {
                        using (var reader = new BinaryReader(remoteStream))
                        {
                            using (var writer = new BinaryWriter(fileStream))
                            {
                                var buffer = reader.ReadBytes(BinaryReaderBufferSize);
                                while (buffer.Length > 0)
                                {
                                    writer.Write(buffer);
                                    buffer = reader.ReadBytes(BinaryReaderBufferSize);
                                }
                                return localPath;
                            }
                        }
                    }
                }
            }
        }

        public Config GetRemote()
        {
            var uri = new Uri(PrepareDownloadPath(String.Format(@"{0}/{1}", _settings.RemoteConfigPath, _settings.RemoteConfigName)));
            try
            {
                using (var client = new WebClient())
                {
                    if (!AppSettings.AllowProxy)
                    {
                        client.Proxy = null;
                    }
                    client.UseDefaultCredentials = true;
                    using (Stream stream = client.OpenRead(uri))
                    {
                        XmlDocument doc = new XmlDocument();
                        doc.Load(stream);
                        XmlElement root = doc.DocumentElement;
                        if (root == null)
                        {
                            throw new Exception("Не удалось найти информацию об обновлениях для продукта.");
                        }
                        XmlNode xmlConfig = root.ChildNodes[0];
                        Config config = new Config();
                        config.Version = XmlConvert.ToInt32(xmlConfig.Attributes["version"].Value);
                        config.ProductId = xmlConfig.Attributes["name"].Value;
                        config.File = xmlConfig.Attributes["file"].Value;
                        return config;
                    }
                }
            }
            catch (UriFormatException ex)
            {
                throw new System.Net.WebException(String.Format("Неправильный формат строки адреса для поиска обновлений '{0}'.", uri), ex);
            }
            catch (Exception ex)
            {
                throw new System.Net.WebException(String.Format("Не удалось прочитать файл с информацией об обновлении \"{0}\"\r\n({1}: {2}).", uri, ex.GetType().ToString(), ex.Message), ex);
            }


        }
        private void document_ValidationEventHandler(object sender, ValidationEventArgs e)
        {
            if (e.Severity == XmlSeverityType.Warning)
                throw new Exception(String.Format("{0}\nНе удалось валидировать файл конфигурации.\n {1}", e.Severity.ToString(), e.Message));
            else if (e.Severity == XmlSeverityType.Error)
                throw new Exception(String.Format("{0}\nНе удалось валидировать файл конфигурации.\n {1}", e.Severity.ToString(), e.Message));
        }

    }
}
