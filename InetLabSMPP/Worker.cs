using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InetLabSMPP
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private FileSystemWatcher _folderWatcher;
        private readonly string _inputFolder;
        private readonly IServiceProvider _services;

        public Worker(ILogger<Worker> logger, IOptions<AppSettings> settings, IServiceProvider services)
        {
            _logger = logger;
            _services = services;
            _inputFolder = settings.Value.LogFolder;
        }

        //protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        //{
        //    while (!stoppingToken.IsCancellationRequested)
        //    {
        //        _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
        //        await Task.Delay(1000, stoppingToken);
        //    }
        //}

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.CompletedTask;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Service Starting");
            return base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Service");
           // _folderWatcher.EnableRaisingEvents = false;
            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _logger.LogInformation("Disposing Service");
           // _folderWatcher.Dispose();
            base.Dispose();
        }
    }
}
