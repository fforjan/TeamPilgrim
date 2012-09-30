using System;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using JustAProgrammer.TeamPilgrim.VisualStudio.Command;
using JustAProgrammer.TeamPilgrim.VisualStudio.Common;
using JustAProgrammer.TeamPilgrim.VisualStudio.Domain.Entities;
using JustAProgrammer.TeamPilgrim.VisualStudio.Providers;

namespace JustAProgrammer.TeamPilgrim.VisualStudio.Model
{
    public class PilgrimProjectCollectionModel : BaseModel
    {
        private readonly PilgrimModel _pilgrimModel;
        private readonly PilgrimProjectCollection _collection;
        private readonly IPilgrimServiceModelProvider _pilgrimServiceModelProvider;

        private readonly OpenSourceControlExplorerCommand _openSourceControlExplorerCommand;

        public PilgrimProjectCollectionModel(PilgrimModel pilgrimModel, PilgrimProjectCollection collection, IPilgrimServiceModelProvider pilgrimServiceModelProvider)
        {
            _pilgrimServiceModelProvider = pilgrimServiceModelProvider;
            _collection = collection;
            _pilgrimModel = pilgrimModel;

            _openSourceControlExplorerCommand = new OpenSourceControlExplorerCommand(this); 

            State = ModelStateEnum.Invalid;
        }

        protected override void OnActivated()
        {
            VerifyCalledOnUiThread();

            if (ThreadPool.QueueUserWorkItem(PopulatePilgrimProjectCollectionModelCallback))
            {
                State = ModelStateEnum.Fetching;
            }
        }

        public OpenSourceControlExplorerCommand OpenSourceControlExplorerCommand
        {
            get { return _openSourceControlExplorerCommand; }
        }

        public PilgrimModel PilgrimModel
        {
            get { return _pilgrimModel; }
        }

        public string Name
        {
            get { return _collection.ProjectCollection.Name; }
        }

        public Uri Uri
        {
            get { return _collection.ProjectCollection.Uri; }
        }

        public bool Offline
        {
            get { return _collection.ProjectCollection.Offline; }
        }

        public bool AutoReconnect
        {
            get { return _collection.ProjectCollection.AutoReconnect; }
        }

        private void PopulatePilgrimProjectCollectionModelCallback(object state)
        {
            PilgrimProject[] projects;
            IPilgrimBuildServiceModelProvider buildServiceModelProvider;
            
            var projectCollectionModel = this;

            if (_pilgrimServiceModelProvider.TryGetProjectsAndBuildServiceProvider(out projects, out buildServiceModelProvider, Uri))
            {
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new ThreadStart(delegate
                    {
                        var pilgrimProjectModels = projects
                            .Select(project => new PilgrimProjectModel(_pilgrimServiceModelProvider, projectCollectionModel._collection, project, new PilgrimProjectBuildModel(_pilgrimServiceModelProvider, buildServiceModelProvider, projectCollectionModel._collection, project)))
                            .ToArray();

                        ProjectModels = pilgrimProjectModels;


                        State = ModelStateEnum.Active;

                        foreach (var pilgrimProjectModel in pilgrimProjectModels)
                        {
                            pilgrimProjectModel.Activate();
                        }
                    }));
            }
            else
            {
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new ThreadStart(delegate
                    {
                        State = ModelStateEnum.Invalid;
                    }));
            }
        }

        #region ProjectModels

        private PilgrimProjectModel[] _projectModels = new PilgrimProjectModel[0];

        public PilgrimProjectModel[] ProjectModels
        {
            get
            {
                VerifyCalledOnUiThread();
                return _projectModels;
            }
            private set
            {
                VerifyCalledOnUiThread();
                if (_projectModels == value) return;

                _projectModels = value;
                SendPropertyChanged("ProjectModels");
            }
        }

        #endregion

    }
}