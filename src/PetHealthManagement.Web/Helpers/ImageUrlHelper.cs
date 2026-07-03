namespace PetHealthManagement.Web.Helpers;

public static class ImageUrlHelper
{
    public const string DefaultAvatarUrl = "/images/default/avatar-placeholder.webp";
    public const string DefaultPetPhotoUrl = "/images/default/pet-placeholder.webp";

    public static string ImageUrl(Guid imageId)
    {
        return $"/images/{imageId:D}";
    }

    public static string ResolveAvatarUrl(Guid? avatarImageId)
    {
        return avatarImageId is null ? DefaultAvatarUrl : ImageUrl(avatarImageId.Value);
    }

    public static string ResolvePetPhotoUrl(Guid? photoImageId)
    {
        return photoImageId is null ? DefaultPetPhotoUrl : ImageUrl(photoImageId.Value);
    }
}
