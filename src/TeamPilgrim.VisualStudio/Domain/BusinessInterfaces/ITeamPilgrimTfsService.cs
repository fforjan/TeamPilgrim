﻿using System;
using JustAProgrammer.TeamPilgrim.VisualStudio.Domain.Entities;
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
        BuildDefinitionWrapper[] QueryBuildDefinitions(TfsTeamProjectCollection collection, string teamProject);
        BuildDetailWrapper[] QueryBuildDetails(TfsTeamProjectCollection collection, string teamProject);
    }
}