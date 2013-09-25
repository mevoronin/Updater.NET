using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using NETUpdater.UI;
using NETUpdater.Properties;

namespace NETUpdater.Update
{
    public class Updater
    {
        private IUpdaterView frm;
        private List<AppArgument> argList;
        private Thread Worker = null;
        private delegate void WorkerStatus(string s);
        private delegate void WorkerEnableForm(bool enable);
        private delegate void WorkerException(Exception ex);
        private delegate void WorkerMessage(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon);
        private delegate void WorkerFinish();

        ConfigLoader updater;
        public Updater(object view)
        {
            frm = view as IUpdaterView;
            argList = new List<AppArgument>();
        }
        /// <summary>
        /// Запись аргументов вызова обновлялки
        /// </summary>
        /// <param name="args">Список аргументов</param>
        public void SetArgs(string[] args)
        {
            foreach (var a in args)
            {
                string[] res = a.Split(new[] { '=' });
                var argName = res[0].Trim();
                var argValue = (res.Length > 1) ? res[1].Trim() : String.Empty;
                if (argName.IndexOf("--") == 0)
                    argList.Add(new AppArgument() { Name = argName, Value = argValue });
            }
        }

        /// <summary>
        /// Процесс после завершения обновления
        /// </summary>
        /// <param name="path">Путь</param>
        private void DoPostUpdateAction(string path)
        {
            var settings = (Settings)Settings.Synchronized(Settings.Default);

            if (!String.IsNullOrEmpty(Settings.Default.PostUpdateAction))
            {
                var filePath = String.Format(@"{1}{0}{2}", Path.DirectorySeparatorChar, path, Settings.Default.PostUpdateAction);
                if (File.Exists(filePath))
                {
                    try
                    {
                        string args = string.Empty;
                        AppArgument postUpdateArgument = argList.Find(x => x.Name == "--execute");
                        if (postUpdateArgument != null) args += postUpdateArgument.Value;
                        Process.Start(filePath, args);
                    }
                    catch { }
                }
            }
        }

        #region статус
        /// <summary>
        /// Обновление статуса процесса
        /// </summary>
        /// <param name="text">Строка описания статуса процесса</param>
        private void SetUpdateStatusAsync(string text)
        {
            frm.Invoke(new WorkerStatus(SetUpdateStatus), new object[] { text });
            Thread.Sleep(10);
        }
        /// <summary>
        /// Обновление статуса процесса
        /// </summary>
        /// <param name="text">Строка описания статуса процесса</param>
        private void SetUpdateStatus(string text)
        {
            frm.ProcessStatus = text;
        }
        #endregion
        #region блокировка формы
        private void EnableFormAsync(bool enable)
        {
            frm.Invoke(new WorkerEnableForm(EnableForm), new object[] { enable });
        }
        private void EnableForm(bool enable)
        {
            frm.EnabledForm = enable;
        }
        #endregion
        #region исключение
        private void HandleUpdateExceptionAsync(Exception ex)
        {
            frm.Invoke(new WorkerException(ShowException), new object[] { ex });
        }
        private void ShowMessage(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            MessageBox.Show(message, title, buttons, icon);
        }
        private void HandleUpdateMessage(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            frm.Invoke(new WorkerMessage(ShowMessage), new object[] { message, title, buttons, icon });
        }
        private void ShowException(Exception ex)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Ошибка обновления!");
            sb.AppendLine();
            sb.AppendLine(String.Format("Текст ошибки: \"{0}\"", ex.Message));
            var typeEx = ex.GetType();
            if (typeEx.Name == "WebException" || typeEx.Name == "UpdaterException")
            {
                MessageBox.Show(sb.ToString(), "Ошибка обновления!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                MessageBox.Show(sb.ToString(), "Ошибка обновления!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion
        private void UpdateCompleted()
        {
            Worker.Join();
            Worker = null;
            frm.CloseForm();
        }

        private UpdaterSettings CreateConfig()
        {
            var settings = (Settings)Settings.Synchronized(Settings.Default);
            return new UpdaterSettings(settings.RemoteConfigPath, settings.RemoteConfigName, settings.PostUpdateAction, settings.ProductName, settings.LocalConfigName, settings.DefaultLocalPackageVersion);
        }
        public void BeginUpdate()
        {
            if (Worker == null)
            {
                Worker = new Thread(new ThreadStart(UpdateAsync));
                Worker.Start();
            }
        }

        private void UpdateAsync()
        {
            AppArgument updatePath = new AppArgument() { Name = "--path", Value = "." };
            AppArgument reset = argList.Find(x => x.Name == "--reset");
            AppArgument checkInstance = argList.Find(x => x.Name == "--processname");
            UpdaterSettings config = CreateConfig();
            try
            {
                SetUpdateStatusAsync("Инициализация...");
                updater = new ConfigLoader(updatePath.Value, config);
                updater.ReadLocal();
                bool needExit = false;
                if (checkInstance != null)
                {
                    Process[] processes = Process.GetProcessesByName(checkInstance.Value);
                    if (processes.Length > 0)
                    {
                        HandleUpdateMessage("Это приложение уже запущено", checkInstance.Value, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        needExit = true;
                    }
                }
                if (!needExit)
                {
                    if (reset != null)
                    {
                        SetUpdateStatusAsync("Выполняется сброс информации об обновлениях...");
                        updater.ResetLocalConfig();
                        SetUpdateStatusAsync("Информация об обновлениях сброшена, завершение работы.");
                    }
                    else
                    {
                        SetUpdateStatusAsync("Загружаю файл с информацией об обновлениях...");
                        if (updater.GetLocal() == null) updater.ResetLocalConfig();
                        Config localConfig = updater.GetLocal();
                        Config remoteConfig = updater.GetRemote();

                        if (localConfig.Version < remoteConfig.Version)
                        {
                            SetUpdateStatusAsync("Обновление найдено, выполняется загрузка...");
                            string localUpdate = updater.DownloadUpdate(remoteConfig);
                            SetUpdateStatusAsync("Выполняется установка обновления...");
                            frm.DisableFormClosing = true;
                            if (ApplyUpdate(localUpdate, remoteConfig.Version))
                            {
                                SetUpdateStatusAsync("Обновление успешно установлено!");
                            }
                            else
                            {
                                SetUpdateStatusAsync("Не удалось установить обновление!");
                            }
                            frm.DisableFormClosing = false;
#if DEBUG
                            Thread.Sleep(3000);
#endif
                        }
                        else
                        {
                            SetUpdateStatusAsync("Oбновление не требуется!");
                        }
                        DoPostUpdateAction(updatePath.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                frm.DisableFormClosing = false;
                HandleUpdateExceptionAsync(ex);
                DoPostUpdateAction(updatePath.Value);
                return;
            }
            finally
            {
                frm.BeginInvoke(new WorkerFinish(UpdateCompleted));
            }
        }
        private bool ApplyUpdate(string localUpdate, int currentVersion)
        {
            if (String.IsNullOrEmpty(localUpdate))
                throw new Exception("Пакет обновления должен быть скачан перед применением");
            var process = Process.Start(localUpdate);
            process.WaitForExit();
            if (process.ExitCode == 0)
            {
                Config config = updater.GetLocal();
                if (config == null) config = new Config() { ProductId = Settings.Default.ProductName };
                config.Version = currentVersion;
                updater.SetLocal(config);
                return true;
            }
            return false;
        }

    }
}
