﻿using System;
using JustAProgrammer.TeamPilgrim.VisualStudio.Business.Services;
using JustAProgrammer.TeamPilgrim.VisualStudio.Domain.BusinessInterfaces;
using JustAProgrammer.TeamPilgrim.VisualStudio.Domain.Entities;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace JustAProgrammer.TeamPilgrim.VisualStudio.Providers
{
    public class PilgrimServiceModelProvider : IPilgrimServiceModelProvider
    {
        private readonly ITeamPilgrimTfsService _teamPilgrimTfsService;

        public PilgrimServiceModelProvider()
        {
            _teamPilgrimTfsService = new TeamPilgrimTfsService();
        }

        public bool TryGetCollections(out TfsTeamProjectCollection[] collections)
        {
            try
            {
                collections = _teamPilgrimTfsService.GetProjectCollections();
                return true;
            }
            catch (Exception) { }

            collections = null;
            return false;
        }

        public bool TryGetCollection(out TfsTeamProjectCollection collection, Uri tpcAddress)
        {
            try
            {
                collection = _teamPilgrimTfsService.GetProjectCollection(tpcAddress);
                return true;
            }
            catch (Exception) { }

            collection = null;
            return false;
        }

        public bool TryGetProjects(out Project[] projects, Uri tpcAddress)
        {
            try
            {
                projects = _teamPilgrimTfsService.GetProjects(tpcAddress);
                return true;
            }
            catch (Exception) { }

            projects = null;
            return false;
        }

        public bool TryDeleteQueryDefinition(out bool result, TfsTeamProjectCollection teamProjectCollection, Project teamProject, Guid queryId)
        {
            try
            {
                result = _teamPilgrimTfsService.DeleteQueryDefintion(teamProjectCollection, teamProject, queryId);
                return true;
            }
            catch (Exception ex)
            {
                
            }

            result = false;
            return false;
        }

        public bool TryGetBuildDefinitionsByProjectName(out BuildDefinitionWrapper[] buildDefinitions, TfsTeamProjectCollection collection, string projectName)
        {
            try
            {
                buildDefinitions = _teamPilgrimTfsService.QueryBuildDefinitions(collection, projectName);
                return true;
            }
            catch (Exception ex)
            {

            }

            buildDefinitions = null;
            return false;
        }
    }
}