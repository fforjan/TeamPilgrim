﻿using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using GalaSoft.MvvmLight.Command;
using JustAProgrammer.TeamPilgrim.VisualStudio.Business.Services.VisualStudio.TeamFoundation;
using JustAProgrammer.TeamPilgrim.VisualStudio.Common;
using JustAProgrammer.TeamPilgrim.VisualStudio.Domain.BusinessInterfaces.VisualStudio;
using JustAProgrammer.TeamPilgrim.VisualStudio.Model.VersionControl;
using JustAProgrammer.TeamPilgrim.VisualStudio.Providers;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace JustAProgrammer.TeamPilgrim.VisualStudio.Model.Core
{
    public class TeamPilgrimServiceModel : BaseServiceModel
    {
        public ObservableCollection<ProjectCollectionServiceModel> ProjectCollectionModels { get; private set; }
        public ObservableCollection<WorkspaceInfoModel> WorkspaceInfoModels { get; private set; }

        private bool _connecting;
        public bool Connecting
        {
            get
            {
                return _connecting;
            }
            private set
            {
                if (_connecting == value) return;

                _connecting = value;

                SendPropertyChanged("Connecting");
            }
        }

        private string _connectingServer;

        public string ConnectingServer
        {
            get
            {
                return _connectingServer;
            }
            private set
            {
                if (_connectingServer == value) return;

                _connectingServer = value;

                SendPropertyChanged("ConnectingServer");
            }
        }

        private ServerConnectedEventArgs.CompletionStatusEnum _connectedStatus;

        public ServerConnectedEventArgs.CompletionStatusEnum ConnectedStatus
        {
            get
            {
                return _connectedStatus;
            }
            private set
            {
                if (_connectedStatus == value) return;

                _connectedStatus = value;

                SendPropertyChanged("ConnectedStatus");
            }
        }

        private Exception _connectedError;

        public Exception ConnectedError
        {
            get
            {
                return _connectedError;
            }
            private set
            {
                if (_connectedError == value) return;

                _connectedError = value;

                SendPropertyChanged("ConnectedError");
            }
        }

        private ProjectCollectionServiceModel _activeProjectCollectionModel = null;

        public ProjectCollectionServiceModel ActiveProjectCollectionModel
        {
            get
            {
                return _activeProjectCollectionModel;
            }
            private set
            {
                if (_activeProjectCollectionModel == value) return;

                _activeProjectCollectionModel = value;

                SendPropertyChanged("ActiveProjectCollectionModel");
            }
        }

        private WorkspaceInfoModel _selectedWorkspaceInfoModel = null;

        public WorkspaceInfoModel SelectedWorkspaceInfoModel
        {
            get
            {
                return _selectedWorkspaceInfoModel;
            }
            private set
            {
                if (_selectedWorkspaceInfoModel == value) return;

                _selectedWorkspaceInfoModel = value;

                if (SelectedWorkspaceInfoModel != null)
                    LoadWorkspaceModel(SelectedWorkspaceInfoModel);

                SendPropertyChanged("SelectedWorkspaceInfoModel");
            }
        }

        private WorkspaceServiceModel _selectedWorkspaceModel;
        public WorkspaceServiceModel SelectedWorkspaceModel
        {
            get
            {
                return _selectedWorkspaceModel;
            }
            private set
            {
                if (_selectedWorkspaceModel == value) return;

                _selectedWorkspaceModel = value;

                SendPropertyChanged("SelectedWorkspaceModel");
            }
        }

        private readonly BackgroundWorker _populateBackgroundWorker;
        private readonly AutoResetEvent _populateResetEvent = new AutoResetEvent(false);

        public TeamPilgrimServiceModel(ITeamPilgrimServiceModelProvider teamPilgrimServiceModelProvider, ITeamPilgrimVsService teamPilgrimVsService)
            : base(teamPilgrimServiceModelProvider, teamPilgrimVsService)
        {
            ProjectCollectionModels = new ObservableCollection<ProjectCollectionServiceModel>();
            WorkspaceInfoModels = new ObservableCollection<WorkspaceInfoModel>();

            ProjectCollectionModels.CollectionChanged += ProjectCollectionModelsOnCollectionChanged;
            WorkspaceInfoModels.CollectionChanged += WorkspaceInfoModelsOnCollectionChanged;

            teamPilgrimVsService.TeamFoundationHost.ContextChanged += TeamFoundationHostOnContextChanged;
            teamPilgrimVsService.TeamFoundationHost.ServerConnecting += delegate(object sender, ServerConnectingEventArgs args)
                {
                    Connecting = true;
                    ConnectingServer = args.TeamProjectCollection != null ? args.TeamProjectCollection.Name : null;
                };

            teamPilgrimVsService.TeamFoundationHost.ServerConnected += delegate(object sender, ServerConnectedEventArgs args)
                {
                    Connecting = false;
                    ConnectingServer = args.TeamProjectCollection != null ? args.TeamProjectCollection.Name : null;
                    ConnectedError = args.Error;
                    ConnectedStatus = args.Status;
                };

            TfsConnectCommand = new RelayCommand(TfsConnect, CanTfsConnect);
            ShowResolveConflicttManagerCommand = new RelayCommand(ShowResolveConflicttManager, CanShowResolveConflicttManager);

            _populateBackgroundWorker = new BackgroundWorker
                {
                    WorkerSupportsCancellation = true
                };
            _populateBackgroundWorker.DoWork += PopulateBackgroundWorkerOnDoWork;

            var activeProjectContext = teamPilgrimVsService.ActiveProjectContext;
            if (activeProjectContext != null &&
                activeProjectContext.DomainUri != null)
            {
                _populateBackgroundWorker.RunWorkerAsync(activeProjectContext.DomainUri);
            }
        }

        private void PopulateBackgroundWorkerOnDoWork(object sender, DoWorkEventArgs doWorkEventArgs)
        {
            try
            {
                var domainUri = (string) doWorkEventArgs.Argument;

                this.Logger().Trace("Begin Populate");

                var tpcAddress = new Uri(domainUri);
                TfsTeamProjectCollection collection;

                if (!teamPilgrimServiceModelProvider.TryGetCollection(out collection, tpcAddress))
                    return;

                Application.Current.Dispatcher.Invoke(() => ProjectCollectionModels.Clear(), DispatcherPriority.Normal);
                if (collection == null)
                    return;

                var projectCollectionServiceModel = new ProjectCollectionServiceModel(teamPilgrimServiceModelProvider,
                                                                                      teamPilgrimVsService, this,
                                                                                      collection);
                Application.Current.Dispatcher.Invoke(() => ProjectCollectionModels.Add(projectCollectionServiceModel));

                WorkspaceInfo[] workspaceInfos;
                if (teamPilgrimServiceModelProvider.TryGetLocalWorkspaceInfos(out workspaceInfos, collection.InstanceId))
                {
                    Application.Current.Dispatcher.Invoke(() => WorkspaceInfoModels.Clear(), DispatcherPriority.Normal);

                    if (_populateBackgroundWorker.CancellationPending)
                    {
                        doWorkEventArgs.Cancel = true;
                        return;
                    }

                    var activeWorkspace = teamPilgrimVsService.ActiveWorkspace;

                    WorkspaceInfoModel selectedWorkspaceInfoModel = null;

                    foreach (var workspaceInfo in workspaceInfos)
                    {
                        var workspaceInfoModel = new WorkspaceInfoModel(workspaceInfo);
                        Application.Current.Dispatcher.Invoke(() => WorkspaceInfoModels.Add(workspaceInfoModel),
                                                              DispatcherPriority.Normal);

                        if (activeWorkspace != null && activeWorkspace.QualifiedName == workspaceInfo.QualifiedName)
                        {
                            selectedWorkspaceInfoModel = workspaceInfoModel;
                        }

                        if (_populateBackgroundWorker.CancellationPending)
                        {
                            doWorkEventArgs.Cancel = true;
                            return;
                        }
                    }

                    if (selectedWorkspaceInfoModel != null)
                    {
                        Application.Current.Dispatcher.Invoke(
                            () => SelectedWorkspaceInfoModel = selectedWorkspaceInfoModel, DispatcherPriority.Normal);
                    }
                }

                this.Logger().Trace("End Populate");
            }
            finally
            {
                _populateResetEvent.Set();
            }
        }

        private void ProjectCollectionModelsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
        {
            ActiveProjectCollectionModel = ProjectCollectionModels.FirstOrDefault();
        }

        private void WorkspaceInfoModelsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
        {
            if (WorkspaceInfoModels.Any())
            {
                if (SelectedWorkspaceInfoModel == null || !WorkspaceInfoModels.Contains(SelectedWorkspaceInfoModel))
                {
                    SelectedWorkspaceInfoModel = WorkspaceInfoModels.First();
                }
            }
            else
            {
                SelectedWorkspaceInfoModel = null;
            }
        }

        private void TeamFoundationHostOnContextChanged(object sender, ContextChangedEventArgs contextChangedEventArgs)
        {
            if (contextChangedEventArgs.TeamProjectCollectionChanged)
            {
                CancelBackgroundWorker();

                var domainUri = (contextChangedEventArgs.NewContext == null) ? null: contextChangedEventArgs.NewContext.DomainUri();
                if (domainUri == null)
                {
                    this.Logger().Trace("Disconnected");
                    Application.Current.Dispatcher.Invoke(() => ProjectCollectionModels.Clear());
                }
                else
                {
                    _populateBackgroundWorker.RunWorkerAsync(domainUri);
                }
            }
            else if (contextChangedEventArgs.TeamProjectChanged)
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var activeProjectContext = teamPilgrimVsService.ActiveProjectContext;

                        foreach (var projectModel in ActiveProjectCollectionModel.ProjectModels)
                        {
                            projectModel.IsActive = projectModel.Project.Name == activeProjectContext.ProjectName;
                        }
                    }, DispatcherPriority.Normal);
            }
        }

        private void CancelBackgroundWorker()
        {
            if (_populateBackgroundWorker.IsBusy)
            {
                this.Logger().Trace("Begin Cancel Populate");

                _populateResetEvent.Reset();
                _populateBackgroundWorker.CancelAsync();
                _populateResetEvent.WaitOne();

                this.Logger().Trace("End Cancel Populate");
            }
        }

        private void LoadWorkspaceModel(WorkspaceInfoModel selectedWorkspaceInfoModel)
        {
            Workspace workspace;
            var projectCollectionModel = ProjectCollectionModels[0];

            Debug.Assert(projectCollectionModel != null, "projectCollectionModel != null");

            if (teamPilgrimServiceModelProvider.TryGetWorkspace(out workspace, selectedWorkspaceInfoModel.WorkspaceInfo, projectCollectionModel.TfsTeamProjectCollection))
            {
                SelectedWorkspaceModel = new WorkspaceServiceModel(teamPilgrimServiceModelProvider, teamPilgrimVsService, this.ActiveProjectCollectionModel, workspace);
            }
        }

        #region Refresh Command

        protected override void Refresh()
        {
            var activeProjectContext = teamPilgrimVsService.ActiveProjectContext;
            if (activeProjectContext != null &&
                activeProjectContext.DomainUri != null)
            {
                _populateBackgroundWorker.RunWorkerAsync(activeProjectContext.DomainUri);
            }
        }

		protected override bool CanRefresh()
        {
            return true;
        }

        #endregion

        #region ShowResolveConflicttManager Command

        public RelayCommand ShowResolveConflicttManagerCommand { get; private set; }

        private void ShowResolveConflicttManager()
        {
            if (SelectedWorkspaceModel == null)
                return;

            var paths =
                SelectedWorkspaceModel.Workspace.Folders
                    .Select(folder => folder.ServerItem).ToArray();

            teamPilgrimVsService.ResolveConflicts(SelectedWorkspaceModel.Workspace, paths, true, false);
        }

        private bool CanShowResolveConflicttManager()
        {
            return true;
        }

        #endregion

        #region TFSConnect Command

        public RelayCommand TfsConnectCommand { get; private set; }

        private void TfsConnect()
        {
            teamPilgrimVsService.TfsConnect();
        }

        private bool CanTfsConnect()
        {
            return true;
        }

        #endregion
    }
}
