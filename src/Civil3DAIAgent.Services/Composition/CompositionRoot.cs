using System;
using Microsoft.Extensions.DependencyInjection;
using Civil3DAIAgent.Application.Workflow;
using Civil3DAIAgent.Application.Workflow.Steps;
using Civil3DAIAgent.Civil3D.Diagnostics;
using Civil3DAIAgent.Civil3D.Services;
using Civil3DAIAgent.Civil3D.Support;
using Civil3DAIAgent.Core.Abstractions;
using Civil3DAIAgent.Core.Workflow;
using Civil3DAIAgent.Infrastructure.Configuration;
using Civil3DAIAgent.Infrastructure.Excel;
using Civil3DAIAgent.Infrastructure.IO;
using Civil3DAIAgent.Infrastructure.Validation;
using Civil3DAIAgent.Logging;
using Civil3DAIAgent.Models.Enums;
using Civil3DAIAgent.Services.Facade;
using IConfigurationProvider = Civil3DAIAgent.Core.Abstractions.IConfigurationProvider;

namespace Civil3DAIAgent.Services.Composition
{
    /// <summary>
    /// The composition root: the single place that maps every abstraction to its concrete
    /// implementation and builds the dependency-injection container. Entry points (the NETLOAD command
    /// and the WPF UI) call <see cref="Build"/> once and resolve <see cref="IAutomationService"/>.
    /// </summary>
    public static class CompositionRoot
    {
        private static readonly object Gate = new object();
        private static IServiceProvider _provider;

        /// <summary>
        /// Builds (once) and returns the application's service provider. Subsequent calls return the
        /// same instance so loggers and configuration are shared across the session.
        /// </summary>
        public static IServiceProvider Build()
        {
            if (_provider != null) return _provider;
            lock (Gate)
            {
                if (_provider != null) return _provider;
                _provider = ConfigureServices().BuildServiceProvider();
                return _provider;
            }
        }

        /// <summary>Resolves the automation facade from the (built) container.</summary>
        public static IAutomationService GetAutomationService() => Build().GetRequiredService<IAutomationService>();

        /// <summary>Registers every service, logger, Civil 3D operation, and workflow step.</summary>
        private static IServiceCollection ConfigureServices()
        {
            var services = new ServiceCollection();

            // ---- Logging ----
            // Permanent sinks = UI window (Information) + an always-on crash log at
            // C:\Temp\Civil3DAIAgent.log (Trace, auto-flushed every line) so the LAST line written
            // before an unmanaged crash identifies the exact failing call. The RoutingLogger also
            // attaches a per-run <output>\logs file during a run.
            services.AddSingleton(new UiLogSink(LogLevel.Information));
            services.AddSingleton(sp =>
            {
                var ui = sp.GetRequiredService<UiLogSink>();
                var crash = new FileLogger(@"C:\Temp\Civil3DAIAgent.log", LogLevel.Trace);
                return new RoutingLogger(new CompositeLogger(ui, crash));
            });
            services.AddSingleton<ILogger>(sp => sp.GetRequiredService<RoutingLogger>());

            // ---- Cross-cutting Civil 3D helpers ----
            services.AddSingleton<IExceptionExplainer, Civil3DExceptionExplainer>();
            services.AddSingleton<ICivilDocProvider, CivilDocProvider>();

            // ---- Infrastructure (config, files, excel, validation) ----
            services.AddSingleton<IConfigurationProvider>(sp =>
                new JsonConfigurationProvider(sp.GetRequiredService<ILogger>()));
            services.AddSingleton<IFileService>(sp => new FileService(sp.GetRequiredService<ILogger>()));
            services.AddSingleton<IExcelPointReader>(sp => new ClosedXmlPointReader(sp.GetRequiredService<ILogger>()));
            services.AddSingleton<IInputValidator>(sp => new InputValidator(sp.GetRequiredService<IFileService>()));

            // ---- Civil 3D operation services (steps 1-23) ----
            services.AddSingleton<IDrawingService, DrawingService>();
            services.AddSingleton<IAlignmentService, AlignmentService>();
            services.AddSingleton<ISurfaceService, SurfaceService>();
            services.AddSingleton<IProfileService, ProfileService>();
            services.AddSingleton<IAssemblyService, AssemblyService>();
            services.AddSingleton<ICorridorService, CorridorService>();
            services.AddSingleton<ISampleLineService, SampleLineService>();
            services.AddSingleton<IMaterialService, MaterialService>();
            services.AddSingleton<IProfileViewService, ProfileViewService>();
            services.AddSingleton<ISectionViewService, SectionViewService>();
            services.AddSingleton<ISheetService, SheetService>();
            services.AddSingleton<ISaveService, SaveService>();
            services.AddSingleton<IPdfPublisher, PdfPublisher>();

            // ---- Workflow steps (each resolved into the engine as IWorkflowStep) ----
            RegisterSteps(services);

            // ---- Engine + facade ----
            services.AddSingleton<IWorkflowEngine, WorkflowEngine>();
            services.AddSingleton<IAutomationService, AutomationService>();

            return services;
        }

        /// <summary>Registers all 23 workflow steps as <see cref="IWorkflowStep"/> implementations.</summary>
        private static void RegisterSteps(IServiceCollection services)
        {
            // Steps 1-6 (drawing).
            services.AddSingleton<IWorkflowStep, OpenSourceDrawingStep>();
            services.AddSingleton<IWorkflowStep, SelectRoadPolylineStep>();
            services.AddSingleton<IWorkflowStep, ExtractFirstSegmentStep>();
            services.AddSingleton<IWorkflowStep, CreateNewDrawingStep>();
            services.AddSingleton<IWorkflowStep, PastePolylineStep>();
            services.AddSingleton<IWorkflowStep, CopyContoursStep>();

            // Steps 7-10 (alignment, surface, profiles).
            services.AddSingleton<IWorkflowStep, CreateAlignmentStep>();
            services.AddSingleton<IWorkflowStep, CreateExistingGroundSurfaceStep>();
            services.AddSingleton<IWorkflowStep, CreateExistingGroundProfileStep>();
            services.AddSingleton<IWorkflowStep, CreateDesignProfileStep>();

            // Steps 11-14 (assembly, corridor, corridor surfaces).
            services.AddSingleton<IWorkflowStep, CreateAssemblyStep>();
            services.AddSingleton<IWorkflowStep, CreateCorridorStep>();
            services.AddSingleton<IWorkflowStep, CreateTopSurfaceStep>();
            services.AddSingleton<IWorkflowStep, CreateDatumSurfaceStep>();

            // Steps 15-19 (sample lines, materials, cut/fill, section & profile views).
            services.AddSingleton<IWorkflowStep, CreateSampleLinesStep>();
            services.AddSingleton<IWorkflowStep, ComputeMaterialsStep>();
            services.AddSingleton<IWorkflowStep, ComputeCutFillStep>();
            services.AddSingleton<IWorkflowStep, CreateSectionViewsStep>();
            services.AddSingleton<IWorkflowStep, CreateProfileViewsStep>();

            // Steps 20-23 (sheets, sheet set, PDF, save).
            services.AddSingleton<IWorkflowStep, GenerateLayoutSheetsStep>();
            services.AddSingleton<IWorkflowStep, CreateSheetSetStep>();
            services.AddSingleton<IWorkflowStep, GeneratePdfsStep>();
            services.AddSingleton<IWorkflowStep, SaveDrawingStep>();
        }
    }
}
