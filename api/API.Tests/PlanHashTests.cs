using System.Text.Json.Nodes;
using Xunit;

namespace API.Tests;

// Slice 3 (Admin-Gated-Automation): Stabilitaets- und Drift-Tests fuer PlanHash.
// Der Hash wird im Plan-Endpoint berechnet und im Approval-Endpoint verglichen;
// jede Inkonsistenz in der Kanonisierung wuerde valide Approvals als Drift abweisen.
public sealed class PlanHashTests
{
    [Fact]
    public void ComputeHash_IsStable_AcrossIdenticalInputs()
    {
        var plan = BuildSamplePlan();

        var hash1 = PlanHash.ComputeHash(plan);
        var hash2 = PlanHash.ComputeHash(plan);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_FormatIs64HexLowercase()
    {
        var plan = BuildSamplePlan();

        var hash = PlanHash.ComputeHash(plan);

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void ComputeHash_DiffersOnPlanChange()
    {
        var planA = new NodePlanResult("nodeA", new[]
        {
            new ActionPlanStep("CreateAdUserLdaps", true, JsonNode.Parse("""{"targetDn":"CN=A"}"""), null)
        });
        var planB = new NodePlanResult("nodeA", new[]
        {
            new ActionPlanStep("CreateAdUserLdaps", true, JsonNode.Parse("""{"targetDn":"CN=B"}"""), null)
        });

        Assert.NotEqual(PlanHash.ComputeHash(planA), PlanHash.ComputeHash(planB));
    }

    [Fact]
    public void ComputeHash_IsKeyOrderInsensitive_WithinObjects()
    {
        // Same logical plan, but JsonNode-Parse produces different internal key orders.
        var planA = new NodePlanResult("nodeA", new[]
        {
            new ActionPlanStep("CreateAdUserLdaps", true,
                JsonNode.Parse("""{"a":1,"b":2,"c":3}"""), null)
        });
        var planB = new NodePlanResult("nodeA", new[]
        {
            new ActionPlanStep("CreateAdUserLdaps", true,
                JsonNode.Parse("""{"c":3,"b":2,"a":1}"""), null)
        });

        Assert.Equal(PlanHash.ComputeHash(planA), PlanHash.ComputeHash(planB));
    }

    [Fact]
    public void ComputeHash_IsArrayOrderSensitive()
    {
        // Array order is semantic (action execution order; group ordering) — must matter.
        var planA = new NodePlanResult("nodeA", new[]
        {
            new ActionPlanStep("First", true, null, null),
            new ActionPlanStep("Second", true, null, null)
        });
        var planB = new NodePlanResult("nodeA", new[]
        {
            new ActionPlanStep("Second", true, null, null),
            new ActionPlanStep("First", true, null, null)
        });

        Assert.NotEqual(PlanHash.ComputeHash(planA), PlanHash.ComputeHash(planB));
    }

    [Fact]
    public void ComputeHash_AlgorithmConstant_IsSha256()
    {
        Assert.Equal("SHA-256", PlanHash.Algorithm);
    }

    private static NodePlanResult BuildSamplePlan() => new(
        "AD-Anlage",
        new[]
        {
            new ActionPlanStep(
                "CreateAdUserLdaps",
                true,
                JsonNode.Parse("""{"alreadyExists":false,"targetDn":"CN=Max,OU=DE"}"""),
                null),
            new ActionPlanStep(
                "AssignGroupsLdaps",
                true,
                JsonNode.Parse("""{"userDistinguishedName":"CN=Max,OU=DE","groups":[{"groupDn":"CN=Group1","alreadyMember":false}]}"""),
                null)
        });
}
