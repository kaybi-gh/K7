using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Restrictions;
using K7.Shared.Dtos.Users;

namespace K7.Shared.Interfaces;

public interface IUserAdminService
{
    Task<List<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<UserDto?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
    Task UpdateUserRoleAsync(Guid userId, UpdateUserRoleRequest request, CancellationToken cancellationToken = default);
    Task UpdateUserCapabilitiesAsync(Guid userId, UpdateUserCapabilitiesRequest request, CancellationToken cancellationToken = default);
    Task ToggleUserActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task UpdateUserLibraryExclusionsAsync(Guid userId, UpdateUserLibraryExclusionsRequest request, CancellationToken cancellationToken = default);
    Task UpdateUserMediaExclusionsAsync(Guid userId, UpdateUserMediaExclusionsRequest request, CancellationToken cancellationToken = default);
    Task<bool> ToggleMediaExclusionAsync(Guid mediaId, CancellationToken cancellationToken = default);
    Task<List<LiteMediaDto>> GetSelfMediaExclusionsAsync(CancellationToken cancellationToken = default);
    Task UpdateUserPinAsync(Guid userId, string? pin, CancellationToken cancellationToken = default);
    Task<List<ContentRestrictionProfileDto>> GetContentRestrictionProfilesAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateContentRestrictionProfileAsync(CreateContentRestrictionProfileRequest request, CancellationToken cancellationToken = default);
    Task UpdateContentRestrictionProfileAsync(Guid id, UpdateContentRestrictionProfileRequest request, CancellationToken cancellationToken = default);
    Task DeleteContentRestrictionProfileAsync(Guid id, CancellationToken cancellationToken = default);
    Task AssignContentRestrictionProfileAsync(Guid userId, Guid? profileId, CancellationToken cancellationToken = default);
    Task<List<RestrictedMediaPreviewDto>> PreviewRestrictedMediasAsync(Guid profileId, CancellationToken cancellationToken = default);
    Task<string?> GetUserLanguageAsync(CancellationToken cancellationToken = default);
    Task UpdateUserLanguageAsync(string language, CancellationToken cancellationToken = default);
    Task<UserDto> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task MergeUsersAsync(Guid sourceUserId, Guid targetUserId, MergeStrategy? strategy = null, CancellationToken cancellationToken = default);
    Task ResetUserPasswordAsync(Guid userId, ResetUserPasswordRequest request, CancellationToken cancellationToken = default);

    // Profile & Account
    Task UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default);
    Task UploadAvatarAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);
    Task RemoveAvatarAsync(CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task SetPasswordAsync(SetPasswordRequest request, CancellationToken cancellationToken = default);
    Task RemovePasswordAsync(RemovePasswordRequest request, CancellationToken cancellationToken = default);
    Task UpdateEmailAsync(UpdateEmailRequest request, CancellationToken cancellationToken = default);
    Task DeleteAccountAsync(DeleteAccountRequest request, CancellationToken cancellationToken = default);
    Task RestoreUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<LoginMethodsDto> GetLoginMethodsAsync(CancellationToken cancellationToken = default);
    Task UnlinkExternalLoginAsync(string provider, CancellationToken cancellationToken = default);
}
