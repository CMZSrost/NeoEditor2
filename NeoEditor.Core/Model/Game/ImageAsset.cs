namespace NeoEditor.Data.Model.Game;

/// <summary>
/// Marker target type for image/sprite reference columns (strIMG / strImg / vImageList /
/// vSpriteList). Not a real game table — there are no ImageAsset entities in the DB.
/// References resolve to "Namespace:FileName" (e.g. "0:AModeSpearSharp.png") and round-trip
/// verbatim; lookups simply miss since no entities of this type are loaded (Doc 37 §2.5).
/// </summary>
public sealed class ImageAsset
{
}
