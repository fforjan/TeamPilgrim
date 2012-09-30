﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using JustAProgrammer.TeamPilgrim.VisualStudio.Domain.BusinessInterfaces;
using JustAProgrammer.TeamPilgrim.VisualStudio.Domain.Entities;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace JustAProgrammer.TeamPilgrim.VisualStudio.Business.Services
{
    public class TeamPilgrimService : ITeamPilgrimService
    {
        private readonly ConcurrentDictionary<string, TfsTeamProjectCollection> _projectCollectionDictionary = new ConcurrentDictionary<string, TfsTeamProjectCollection>();

        private TfsTeamProjectCollection GetTfsTeamProjectCollection(Uri uri)
        {
            return _projectCollectionDictionary.GetOrAdd(uri.ToString(), s =>
                {
                    var tfsTeamProjectCollection = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(uri);
                    tfsTeamProjectCollection.Authenticate();

                    return tfsTeamProjectCollection;
                });
        }

        public PilgrimProjectCollection[] GetPilgrimProjectCollections()
        {
            var registeredProjectCollections = RegisteredTfsConnections.GetProjectCollections();

            return registeredProjectCollections.Select(collection => new PilgrimProjectCollection
                {
                    ProjectCollection = collection
                }).ToArray();
        }

        public PilgrimProject[] GetPilgrimProjects(Uri tpcAddress)
        {
            return GetPilgrimProjects(GetTfsTeamProjectCollection(tpcAddress));
        }

        private PilgrimProject[] GetPilgrimProjects(TfsTeamProjectCollection tfsTeamProjectCollection)
        {
            var workItemStore = new WorkItemStore(tfsTeamProjectCollection);

            return (from object project in workItemStore.Projects
                    select new PilgrimProject

                        {
                            Project = (Project) project
                        }).ToArray();
        }

        public ITeamPilgrimBuildService GetTeamPilgrimBuildService(Uri tpcAddress)
        {
            var collection = GetTfsTeamProjectCollection(tpcAddress);
            return GetTeamPilgrimBuildService(collection);
        }

        private ITeamPilgrimBuildService GetTeamPilgrimBuildService(TfsTeamProjectCollection collection)
        {
            var buildServer = collection.GetService<IBuildServer>();

            return new TeamPilgrimBuildService(buildServer);
        }
    }
}