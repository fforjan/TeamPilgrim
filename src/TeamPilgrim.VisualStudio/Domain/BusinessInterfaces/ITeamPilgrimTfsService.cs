﻿using System;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace JustAProgrammer.TeamPilgrim.VisualStudio.Domain.BusinessInterfaces
{
    public interface ITeamPilgrimTfsService
    {
        TfsTeamProjectCollection[] GetProjectCollections();
        TfsTeamProjectCollection GetProjectCollection(Uri uri);

        Project[] GetProjects(Uri tpcAddress);
        RegisteredProjectCollection[] GetRegisteredProjectCollections();

        bool DeleteQueryDefintion(TfsTeamProjectCollection teamProjectCollection, Project teamProject, Guid queryId);
        IBuildDefinition[] QueryBuildDefinitions(TfsTeamProjectCollection collection, string teamProject);
        IBuildDetail[] QueryBuildDetails(TfsTeamProjectCollection collection, string teamProject);
		
		IBuildDefinition CloneBuildDefinition(TfsTeamProjectCollection collection, string projectName, IBuildDefinition sourceDefinition);
        void DeleteBuildDefinition(IBuildDefinition buildDefinition);
    }
}