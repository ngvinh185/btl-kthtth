namespace BTL_WEB.Services;

public interface IAdoptionWorkflowService
{
    Task<(bool Success, string? ErrorMessage, int? RequestId)> CreateRequestAsync(int currentUserId, BTL_WEB.ViewModels.Adoption.CreateAdoptionRequestViewModel model);
    Task<(bool Success, string? ErrorMessage)> ReviewRequestAsync(int reviewerUserId, BTL_WEB.ViewModels.Adoption.ReviewAdoptionRequestViewModel model);
}
