using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using EnvDTE;
using EnvDTE80;
using JustAProgrammer.TeamPilgrim.VisualStudio.Business.Services.VisualStudio.Builds;
using JustAProgrammer.TeamPilgrim.VisualStudio.Business.Services.VisualStudio.TeamFoundation;
using JustAProgrammer.TeamPilgrim.VisualStudio.Business.Services.VisualStudio.VersionControl;
using JustAProgrammer.TeamPilgrim.VisualStudio.Business.Services.VisualStudio.WorkItems;
using JustAProgrammer.TeamPilgrim.VisualStudio.Common;
using JustAProgrammer.TeamPilgrim.VisualStudio.Common.Extensions;
using JustAProgrammer.TeamPilgrim.VisualStudio.Domain.BusinessInterfaces.VisualStudio;
using Microsoft.TeamFoundation;
using Microsoft.TeamFoundation.Build.Common;
using Microsoft.TeamFoundation.Build.Controls;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Controls.WinForms;
using Microsoft.TeamFoundation.Server;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Controls;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TeamFoundation;
using Microsoft.VisualStudio.TeamFoundation.Build;
using Microsoft.VisualStudio.TeamFoundation.VersionControl;
using Microsoft.VisualStudio.TeamFoundation.WorkItemTracking;
using NLog;
using Project = Microsoft.TeamFoundation.WorkItemTracking.Client.Project;
using ProjectState = Microsoft.TeamFoundation.Common.ProjectState;

namespace JustAProgrammer.TeamPilgrim.VisualStudio.Business.Services.VisualStudio
{
    public class TeamPilgrimVsService : ITeamPilgrimVsService, IVsSolutionEvents
    {
        private static readonly Logger Logger = TeamPilgrimLogManager.Instance.GetCurrentClassLogger();

        protected IVsUIShell VsUiShell { get; private set; }

        protected DTE2 Dte2 { get; set; }

        protected IVsSolution VsSolution { get; set; }

        protected TeamFoundationServerExt TeamFoundationServerExt { get; set; }
        private static readonly Lazy<FieldInfo> TeamFoundationServerExt_TeamFoundationHostField;

        public ITeamFoundationHostWrapper TeamFoundationHost { get; private set; }

        protected VersionControlExt VersionControlExt { get; set; }

        protected DocumentServiceWrapper WorkItemTrackingDocumentService { get; set; }

        public event SolutionStateChanged SolutionStateChanged;

        public ProjectContextExt ActiveProjectContext
        {
            get
            {
                return TeamFoundationServerExt == null ? null : TeamFoundationServerExt.ActiveProjectContext;
            }
        }

        public Workspace ActiveWorkspace
        {
            get { return VersionControlExt.Explorer.Workspace; }
        }

        public Solution Solution
        {
            get { return Dte2.Solution; }
        }

        private TeamPilgrimPackage _packageInstance;

        private readonly Lazy<VsTeamFoundationBuildWrapper> _teamFoundationBuild;

        private readonly Lazy<WorkItemTrackingPackageWrapper> _workItemTrackingPackage;

        private readonly Lazy<QuerySecurityCommandHelpersWrapper> _querySecurityCommandHelpers;

        private readonly Lazy<VersionControlPackageWrapper> _versionControlPackage;

        private readonly Lazy<PendingChangesPageViewModelUtilsWrapper> _pendingChangesPageViewModelUtilsWrapper;

        private readonly Lazy<IPortalSettingsLauncher> _portalSettingsLauncher;

        private readonly Lazy<ISourceControlSettingsLauncher> _sourceControlSettingsLauncher;

        private readonly Lazy<IProcessTemplateManagerLauncher> _processTemplateManagerLauncher;

        private IWorkItemControlHost _workItemControlHost;

        private readonly uint _adviseSolutionEventsCookie;

        static TeamPilgrimVsService()
        {
            TeamFoundationServerExt_TeamFoundationHostField = new Lazy<FieldInfo>(() => typeof(TeamFoundationServerExt).GetField("m_teamFoundationHost", BindingFlags.NonPublic | BindingFlags.Instance));
        }

        public TeamPilgrimVsService(TeamPilgrimPackage packageInstance, IVsUIShell vsUiShell, DTE2 dte2, IVsSolution vsSolution)
        {
            _teamFoundationBuild = new Lazy<VsTeamFoundationBuildWrapper>(() => new VsTeamFoundationBuildWrapper(_packageInstance.GetPackageService<IVsTeamFoundationBuild>()));
            _portalSettingsLauncher = new Lazy<IPortalSettingsLauncher>(() => _packageInstance.GetPackageService<IPortalSettingsLauncher>());
            _sourceControlSettingsLauncher = new Lazy<ISourceControlSettingsLauncher>(() => _packageInstance.GetPackageService<ISourceControlSettingsLauncher>());
            _processTemplateManagerLauncher = new Lazy<IProcessTemplateManagerLauncher>(() => _packageInstance.GetPackageService<IProcessTemplateManagerLauncher>());
            _workItemTrackingPackage = new Lazy<WorkItemTrackingPackageWrapper>();
            _versionControlPackage = new Lazy<VersionControlPackageWrapper>();
            _querySecurityCommandHelpers = new Lazy<QuerySecurityCommandHelpersWrapper>();
            _pendingChangesPageViewModelUtilsWrapper = new Lazy<PendingChangesPageViewModelUtilsWrapper>();

            VsUiShell = vsUiShell;
            _packageInstance = packageInstance;
            Dte2 = dte2;
            VsSolution = vsSolution;
            VersionControlExt = dte2.GetObject("Microsoft.VisualStudio.TeamFoundation.VersionControl.VersionControlExt") as VersionControlExt;
            TeamFoundationServerExt = dte2.GetObject("Microsoft.VisualStudio.TeamFoundation.TeamFoundationServerExt") as TeamFoundationServerExt;
            WorkItemTrackingDocumentService = new DocumentServiceWrapper(dte2.GetObject("Microsoft.VisualStudio.TeamFoundation.WorkItemTracking.DocumentService") as DocumentService);

            var teamFoundationHostObject = (ITeamFoundationContextManager)TeamFoundationServerExt_TeamFoundationHostField.Value.GetValue(TeamFoundationServerExt);
            TeamFoundationHost = new TeamFoundationHostWrapper(teamFoundationHostObject);

            vsSolution.AdviseSolutionEvents(this, out _adviseSolutionEventsCookie);
        }

        private IWorkItemControlHost WorkItemControlHost
        {
            get
            {
                return _workItemControlHost ?? (_workItemControlHost = _packageInstance.GetPackageService<IWorkItemControlHost>());
            }
        }

        public string[] GetSolutionFilePaths()
        {
            Logger.Trace("GetSolutionFilePaths");

            Solution solution = Dte2.Solution;
            var solutionFilePaths = new List<string>
                {
                    solution.FileName
                };

            var projects = solution.Projects.Cast<EnvDTE.Project>().ToArray();

            foreach (var project in projects)
            {
                PopulateProject(solutionFilePaths, project);
            }

            return solutionFilePaths.ToArray();
        }

        private static void PopulateProject(List<string> solutionFilePaths, EnvDTE.Project project)
        {
            var vsProjectKindSolutionItems = project.Kind == EnvDTE.Constants.vsProjectKindSolutionItems;
            var vsProjectKindUnmodeled = project.Kind == EnvDTE.Constants.vsProjectKindUnmodeled;

            if (vsProjectKindUnmodeled)
            {
                Logger.Debug("Project Not Yet Loaded: {0}", project.Name);
                return;
            }

            if (vsProjectKindSolutionItems)
            {
                Logger.Trace("Solution Folder: {0}", project.Name);

                foreach (var projectItem in project.ProjectItems.Cast<ProjectItem>())
                {
                    var vsProjectItemKindSolutionItems = projectItem.Kind == EnvDTE.Constants.vsProjectItemKindSolutionItems;

                    if (vsProjectItemKindSolutionItems)
                    {
                        if (projectItem.SubProject != null)
                        {
                            PopulateProject(solutionFilePaths, projectItem.SubProject);
                        }
                        else
                        {
                            Debug.Assert(projectItem.FileCount == 1);

                            var fileName = projectItem.FileNames[1];

                            if (fileName != null)
                            {
                                solutionFilePaths.Add(fileName);
                            }
                            else
                            {
                                Logger.Debug("Project Not Yet Loaded: {0}", projectItem.Name);
                            }
                        }
                    }
                }
            }
            else
            {
                Logger.Trace("Project: {0}", project.FileName);
                
                solutionFilePaths.Add(project.FileName);
                
                foreach (var projectItem in project.ProjectItems.Cast<ProjectItem>())
                {
                    PopulateChildProjectItems(solutionFilePaths, projectItem);
                }
            }
        }

        private static void PopulateChildProjectItems(List<string> result, ProjectItem item)
        {
            var path = item.FileNames[0];
            try
            {
                var fileAttributes = File.GetAttributes(path);
                if ((fileAttributes & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    var items = item.ProjectItems.GetEnumerator();
                    while (items.MoveNext())
                    {
                        var currentItem = (ProjectItem)items.Current;
                        PopulateChildProjectItems(result, currentItem);
                    }
                }

                result.Add(path);
            }
            catch (Exception)
            {
                result.Add(path);
            }
        }

        public void CompareChangesetChangesWithLatestVersions(IList<PendingChange> pendingChanges)
        {
            _pendingChangesPageViewModelUtilsWrapper.Value.CompareWithLatestVersion(pendingChanges);
        }

        public void CompareChangesetChangesWithWorkspaceVersions(Workspace workspace, IList<PendingChange> pendingChanges)
        {
            _pendingChangesPageViewModelUtilsWrapper.Value.CompareWithWorkspaceVersion(workspace, pendingChanges);
        }

        public void UndoChanges(Workspace workspace, IList<PendingChange> pendingChanges)
        {
            _pendingChangesPageViewModelUtilsWrapper.Value.UndoChanges(workspace, pendingChanges);
        }

        public void View(Workspace workspace, IList<PendingChange> pendingChanges)
        {
            _pendingChangesPageViewModelUtilsWrapper.Value.View(workspace, pendingChanges);
        }

        public void OpenSourceControl(string projectName)
        {
            VersionControlExplorerExt versionControlExplorerExt = VersionControlExt.Explorer;
            versionControlExplorerExt.Navigate("$/" + projectName);
        }

        public void NewQueryDefinition(Project project, QueryFolder parent)
        {
            parent = parent ?? WorkItemTrackingDocumentService.GetDefaultParent(project, false);
            _workItemTrackingPackage.Value.NewQuery(project.Name, parent);
        }

        public void EditQueryDefinition(TfsTeamProjectCollection projectCollection, Guid queryDefinitionId)
        {
            var queryDocument = WorkItemTrackingDocumentService.GetQuery(projectCollection, queryDefinitionId.ToString(), this);

            WorkItemTrackingDocumentService.ShowQuery(queryDocument);
        }

        public void CloseQueryDefinitionFrames(TfsTeamProjectCollection projectCollection, Guid queryDefinitionId)
        {
            var workItemsQueryFrame = GetVsWindowFrameByTypeAndMoniker(VsWindowFrameEditorTypeIds.WorkItemsQueryView, "vstfs:///WorkItemTracking/Query/" + queryDefinitionId.ToString());

            if (workItemsQueryFrame != null)
                workItemsQueryFrame.CloseFrame((int)__FRAMECLOSE.FRAMECLOSE_NoSave);

            var workItemsResultsViewFrame = GetVsWindowFrameByTypeAndMoniker(VsWindowFrameEditorTypeIds.WorkItemsResultView, "vstfs:///WorkItemTracking/Results/" + queryDefinitionId.ToString());

            if (workItemsResultsViewFrame != null)
                workItemsResultsViewFrame.CloseFrame((int)__FRAMECLOSE.FRAMECLOSE_NoSave);
        }

        public void OpenSecurityItemDialog(QueryItem queryItem)
        {
            _querySecurityCommandHelpers.Value.HandleSecurityCommand(queryItem);
        }

        public void ResolveConflicts(Workspace workspace, string[] paths, bool recursive, bool afterCheckin)
        {
            _versionControlPackage.Value.ResolveConflicts(workspace, paths, recursive, afterCheckin);
        }

        private IVsWindowFrame GetVsWindowFrameByTypeAndMoniker(Guid editorTypeId, string moniker)
        {
            IEnumWindowFrames enumWindowFrames;
            VsUiShell.GetDocumentWindowEnum(out enumWindowFrames);

            var frames = new IVsWindowFrame[1];
            uint numFrames;

            IVsWindowFrame m_frame;
            while (enumWindowFrames.Next(1, frames, out numFrames) == VSConstants.S_OK && numFrames == 1)
            {
                m_frame = frames[0] as IVsWindowFrame;

                object monikerObject;
                m_frame.GetProperty((int)__VSFPROPID.VSFPROPID_pszMkDocument, out monikerObject);

                Guid editorTypeIdGuid;
                m_frame.GetGuidProperty((int)__VSFPROPID.VSFPROPID_guidEditorType, out editorTypeIdGuid);

                if (monikerObject != null && monikerObject.Equals(moniker) && editorTypeIdGuid.Equals(editorTypeId))
                {
                    return m_frame;
                }
            }

            return null;
        }

        public void OpenBuildDefinition(Uri buildDefinitionUri)
        {
            _teamFoundationBuild.Value.DetailsManager.OpenBuild(buildDefinitionUri);
        }

        public void QueueBuild(string projectName, Uri buildDefinitionUri)
        {
            _teamFoundationBuild.Value.DetailsManager.QueueBuild(projectName, buildDefinitionUri);
        }

        public void OpenProcessFileLocation(Uri buildDefinitionUri)
        {
            _teamFoundationBuild.Value.BuildExplorerWrapper.NavigateToProcessFile(buildDefinitionUri);
        }

        public void NewBuildDefinition(string projectName)
        {
            _teamFoundationBuild.Value.BuildExplorerWrapper.AddBuildDefinition(projectName);
        }

        public void OpenControllerAgentManager(string projectName)
        {
            _teamFoundationBuild.Value.BuildExplorerWrapper.OpenControllerAgentManager(projectName);
        }

        public void OpenQualityManager(string projectName)
        {
            _teamFoundationBuild.Value.BuildExplorerWrapper.OpenQualityManager(projectName);
        }

        public void OpenBuildSecurityDialog(string projectName, string projectUri)
        {
            var projectInfo = new ProjectInfo(projectUri, projectName, ProjectState.WellFormed);
            var artifactId = LinkingUtilities.DecodeUri(projectUri);
            _teamFoundationBuild.Value.BuildExplorerWrapper.OpenBuildSecurityDialog(projectInfo, projectInfo.Name, artifactId.ToolSpecificId);
        }

        public void OpenBuildDefinitionSecurityDialog(string projectName, string projectUri, string definitionName, string definitionUri)
        {
            var projectInfo = new ProjectInfo(projectUri, projectName, ProjectState.WellFormed);

            var projectArtifactId = LinkingUtilities.DecodeUri(projectUri);
            var definitionArtifactId = LinkingUtilities.DecodeUri(definitionUri);

            var securityToken = string.Concat(projectArtifactId.ToolSpecificId, BuildSecurity.NamespaceSeparator, definitionArtifactId.ToolSpecificId);

            _teamFoundationBuild.Value.BuildExplorerWrapper.OpenBuildSecurityDialog(projectInfo, definitionName, securityToken);
        }

        public void ViewBuilds(string projectName, string buildDefinition, string qualityFilter, DateFilter dateFilter)
        {
            _teamFoundationBuild.Value.BuildExplorer.CompletedView.Show(projectName, buildDefinition, qualityFilter, dateFilter);
        }

        public void GoToWorkItem()
        {
            _workItemTrackingPackage.Value.GoToWorkItem();
        }

        public void NewWorkItem(TfsTeamProjectCollection projectCollection, string projectName, string typeName)
        {
            _workItemTrackingPackage.Value.OpenNewWorkItem(projectCollection, projectName, typeName);
        }

        public void TfsConnect()
        {
            TeamFoundationHost.PromptForServerAndProjects(false);
        }

        public void ShowWorkItemsAreasAndIterationsDialog(TfsTeamProjectCollection tfsTeamProjectCollection, string projectName, string projectUri)
        {
            var classificationAdminUi = new ClassificationAdminUi(tfsTeamProjectCollection, projectName, projectUri);
            classificationAdminUi.ShowDialog();
        }

        public void ShowPortalSettings(TfsTeamProjectCollection tfsTeamProjectCollection, string projectName, string projectUri)
        {
            _portalSettingsLauncher.Value.Show(tfsTeamProjectCollection, projectUri, projectName);
        }

        public void ShowSourceControlSettings()
        {
            _sourceControlSettingsLauncher.Value.LaunchSourceControlSettings();
        }

        public void ShowSourceControlCollectionSettings()
        {
            _sourceControlSettingsLauncher.Value.LaunchSourceControlCollectionSettings();
        }

        public void DisconnectFromTfs()
        {
            Dte2.ExecuteCommand("Team.DisconnectfromTeamFoundationServer");
        }

        public void NewTeamProject()
        {
            Dte2.ExecuteCommand("File.NewTeamProject");
        }

        public void ShowProcessTemplateManager(TfsTeamProjectCollection tfsTeamProjectCollection)
        {
            _processTemplateManagerLauncher.Value.Show(tfsTeamProjectCollection);
        }

        #region IVsSolutionEvents

        int IVsSolutionEvents.OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
        {
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
        {
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
        {
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
        {
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            Logger.Trace("OnAfterOpenSolution");
            if (SolutionStateChanged != null)
                SolutionStateChanged();

            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnQueryCloseSolution(object pUnkReserved, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnBeforeCloseSolution(object pUnkReserved)
        {
            return VSConstants.S_OK;
        }

        int IVsSolutionEvents.OnAfterCloseSolution(object pUnkReserved)
        {
            Logger.Trace("OnAfterCloseSolution");
            if (SolutionStateChanged != null)
                SolutionStateChanged();

            return VSConstants.S_OK;
        }

        #endregion
    }
}