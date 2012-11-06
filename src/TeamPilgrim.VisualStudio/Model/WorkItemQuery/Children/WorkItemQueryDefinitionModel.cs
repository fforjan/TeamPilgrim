using System;
using JustAProgrammer.TeamPilgrim.VisualStudio.Domain.BusinessInterfaces;
using JustAProgrammer.TeamPilgrim.VisualStudio.Providers;
using Microsoft.TeamFoundation.WorkItemTracking.Client;

namespace JustAProgrammer.TeamPilgrim.VisualStudio.Model.WorkItemQuery.Children
{
    public class WorkItemQueryDefinitionModel : WorkItemQueryChildModel
    {
        public QueryDefinition QueryDefinition { get; private set; }

        public WorkItemQueryDefinitionModel(IPilgrimServiceModelProvider pilgrimServiceModelProvider, ITeamPilgrimVsService teamPilgrimVsService, IWorkItemQueryCommandModel workItemQueryCommandModel, int depth, QueryDefinition queryDefinition)
            : base(pilgrimServiceModelProvider, teamPilgrimVsService, workItemQueryCommandModel, depth)
        {
            QueryDefinition = queryDefinition;
        }

        public override Guid Id
        {
            get { return QueryDefinition.Id; }
        }

        public string Name
        {
            get { return QueryDefinition.Name; }
        }
    }
}