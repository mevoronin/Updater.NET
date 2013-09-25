
namespace NETUpdater.Update
{
    public class UpdaterSettings
    {
        public UpdaterSettings(
            string remConfigPath,
            string remConfigName,
            string postUpdateAction,
            string productName,
            string localConfigName,
            int defaultLocalPackageVersion
            )
        {
            RemoteConfigPath = remConfigPath;
            RemoteConfigName = remConfigName;
            PostUpdateAction = postUpdateAction;
            ProductId = productName;
            LocalConfigName = localConfigName;
            DefaultLocalPackageVersion = defaultLocalPackageVersion;
        }


        public string RemoteConfigPath { get; set; }
        public string RemoteConfigName { get; set; }
        public string PostUpdateAction { get; set; }
        public string ProductId { get; set; }
        public string LocalConfigName { get; set; }
        public int DefaultLocalPackageVersion { get; set; }

    }
}
