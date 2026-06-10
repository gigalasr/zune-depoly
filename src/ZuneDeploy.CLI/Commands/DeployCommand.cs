using ZuneDeploy.Transport;
using ZuneDeploy.XNA.Channels;
using ZuneDeploy.XNA.Data;

namespace ZuneDeploy.CLI.Commands;

internal static class DeployVerb {
    public static void DeployCommand(DirectoryInfo folder, bool launch, bool printContainerInfo) {
        try {
            var container = Spinner.SpinFor($"Importing '{folder.Name}'",
                () => ApplicationContainer.FromFolder(folder)
            );

            if (printContainerInfo) {
                Console.Write(container.ToString());
            }

            using Zune zune = Spinner.SpinFor("Connecting to Zune",
                () => new Zune(),
                z => $"Connected to {z.DeviceFamily.AsWellKnownName()}"
            );

            // TODO: Make sure runtime container is available
            // TODO: Deploy runtime, if not available

            Spinner.Start("Opening Deploy Channel");
            using (GameDeployChannel deployChan = zune.OpenXNAGameDeployChannel()) {
                Spinner.SpinFor("Opening App Container", () => {
                    deployChan.OpenContainer(container);
                });

                foreach (var file in container.Files) {
                    DeployFile(deployChan, file);
                }

                if (container.Thumbnail != null) {
                    DeployFile(deployChan, container.Thumbnail, true);
                }

                Spinner.SpinFor("Uploading Container Metadata", () => deployChan.PutGamePropertiesEx(container));
                Spinner.SpinFor("Closing Game Container", deployChan.CloseGameContainer);
            }

            if (launch) {
                LaunchApplication(zune, container);
            }
        } catch (ContainerImportException e) {
            Spinner.Stop($"Failed to import application container: {e.Message}");
        } catch (Exception e) {
            Spinner.Stop($"Deploy Failed: {e.Message}", true);
        }
    }

    public static void DeployFile(GameDeployChannel channel, ContainerFile file, bool isThumbnail = false) {
        Spinner.Start($"Deploying {file.PathInContainer}");
        long maxBytes = 0;
        long totalBytes = 0;
        string currentFilePath = "n/a";
        void SpinnerUpdate(object? _, ushort bytes) {
            totalBytes += bytes;
            int progress = (int)(totalBytes / (double)maxBytes * 100);
            Spinner.SetLabel($"Deploying {currentFilePath} ({progress}%)");
        }

        channel.OnBytesWritten += SpinnerUpdate;
        try {
            using var fs = file.Open();
            currentFilePath = file.PathInContainer;
            maxBytes = fs.Length;
            totalBytes = 0;
            if (isThumbnail) {
                channel.PutThumbnailInContainer(fs);
            } else {
                channel.PutFileInContainer(file.PathInContainer, fs);
            }
        } catch (Exception e) {
            Spinner.Stop($"File Deploy Failed: {e.Message}", true);
        } finally {
            channel.OnBytesWritten -= SpinnerUpdate;
        }

        Spinner.Stop($"Deployed {file.PathInContainer}");
    }

    public static void LaunchApplication(Zune zune, ApplicationContainer container) {
        using LaunchChannel launchChan = zune.OpenXnaLaunchChannel();
        Spinner.SpinFor("Launching Application", () => {
            launchChan.LaunchInMode(container.ContainerId, "", true);
        });
        Spinner.SpinFor("Running", () => {
            do {
                Thread.Sleep(1000);
            } while (launchChan.IsTitleRunning());
        }, "Done");
    }
}
