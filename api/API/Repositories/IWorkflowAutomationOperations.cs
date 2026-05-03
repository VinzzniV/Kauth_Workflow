using Npgsql;

namespace API;

internal interface IWorkflowAutomationOperations
{
    Task<bool> ActionDefinitionExists(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string actionKey,
        bool requireActive);

    Task<long> ResolveActionDefinitionIdByKey(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string actionKey,
        bool requireActive);
}
