namespace TaskFlow.Api.Models.Requests;

public sealed record SavePlannerSceneRequest(
    int ExpectedRevision,
    string SceneJson);
