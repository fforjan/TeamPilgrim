using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using GalaSoft.MvvmLight.Command;
using JustAProgrammer.TeamPilgrim.VisualStudio.Business.Services.VisualStudio.TeamFoundation;
using JustAProgrammer.TeamPilgrim.VisualStudio.Domain.BusinessInterfaces.VisualStudio;
using JustAProgrammer.TeamPilgrim.VisualStudio.Providers;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace JustAProgrammer.TeamPilgrim.VisualStudio.Model.Explorer
{
    public class ProjectCollectionServiceModel : BaseServiceModel
    {
        private readonly TeamPilgrimServiceModel _teamPilgrimServiceModel;

        public ObservableCollection<ProjectServiceModel> ProjectModels { get; private set; }

        public TfsTeamProjectCollection TfsTeamProjectCollection { get; private set; }

        public ProjectCollectionServiceModel(ITeamPilgrimServiceModelProvider teamPilgrimServiceModelProvider, ITeamPilgrimVsService teamPilgrimVsService, TeamPilgrimServiceModel teamPilgrimServiceModel, TfsTeamProjectCollection pilgrimProjectCollection)
            : base(teamPilgrimServiceModelProvider, teamPilgrimVsService)
        {
            ProjectModels = new ObservableCollection<ProjectServiceModel>();

            TfsTeamProjectCollection = pilgrimProjectCollection;
            _teamPilgrimServiceModel = teamPilgrimServiceModel;

            DisconnectCommand = new RelayCommand(Disconnect, CanDisconnect);
            NewTeamProjectCommand = new RelayCommand(NewTeamProject, CanNewTeamProject);
            OpenGroupMembershipCommand = new RelayCommand(OpenGroupMembership, CanOpenGroupMembership);
            ShowProcessTemplateManagerCommand = new RelayCommand(ShowProcessTemplateManager, CanShowProcessTemplateManager);
            ShowSecuritySettingsCommand = new RelayCommand(ShowSecuritySettings, CanShowSecuritySettings);
            OpenSourceControlSettingsCommand = new RelayCommand(OpenSourceControlSettings, CanOpenSourceControlSettings);

            Populate();
        }

        private void Populate()
        {
            Project[] projects;
            if (teamPilgrimServiceModelProvider.TryGetProjects(out projects, TfsTeamProjectCollection.Uri))
            {
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new ThreadStart(delegate
                    {
                        ProjectModels.Clear();

                        var pilgrimProjectModels = projects
                            .Select(
                                project =>
                                new ProjectServiceModel(teamPilgrimServiceModelProvider, teamPilgrimVsService,
                                                        _teamPilgrimServiceModel, TfsTeamProjectCollection, project));

                        foreach (var pilgrimProjectModel in pilgrimProjectModels)
                        {
                            ProjectModels.Add(pilgrimProjectModel);
                        }
                    }));
            }
        }

        #region Refresh Command

        protected override void Refresh()
        {
            Populate();
        }

        protected override bool CanRefresh()
        {
            return true;
        }

        #endregion

        #region Disconnect Command

        public RelayCommand DisconnectCommand { get; private set; }

        private void Disconnect()
        {
            teamPilgrimVsService.DisconnectFromTfs();
        }

        private bool CanDisconnect()
        {
            return true;
        }

        #endregion

        #region NewTeamProject Command

        public RelayCommand NewTeamProjectCommand { get; private set; }

        private void NewTeamProject()
        {
            teamPilgrimVsService.NewTeamProject();
        }

        private bool CanNewTeamProject()
        {
            return true;
        }

        #endregion

        #region ShowSecuritySettings Command

        public RelayCommand ShowSecuritySettingsCommand { get; private set; }

        private void ShowSecuritySettings()
        {
            CommandHandlerPackageWrapper.OpenSecuritySettings(TfsTeamProjectCollection, null, null);
        }

        private bool CanShowSecuritySettings()
        {
            return true;
        }

        #endregion

        #region OpenGroupMembership Command

        public RelayCommand OpenGroupMembershipCommand { get; private set; }

        private void OpenGroupMembership()
        {
            CommandHandlerPackageWrapper.OpenGroupMembership(TfsTeamProjectCollection, null, null);
        }

        private bool CanOpenGroupMembership()
        {
            return true;
        }

        #endregion

        #region ShowProcessTemplateManager Command

        public RelayCommand ShowProcessTemplateManagerCommand { get; private set; }

        private void ShowProcessTemplateManager()
        {
            teamPilgrimVsService.ShowProcessTemplateManager(TfsTeamProjectCollection);
        }

        private bool CanShowProcessTemplateManager()
        {
            return true;
        }

        #endregion

        #region OpenSourceControlSettings Command

        public RelayCommand OpenSourceControlSettingsCommand { get; private set; }

        private void OpenSourceControlSettings()
        {
            teamPilgrimVsService.ShowSourceControlCollectionSettings();
        }

        private bool CanOpenSourceControlSettings()
        {
            return true;
        }

        #endregion
    }
}