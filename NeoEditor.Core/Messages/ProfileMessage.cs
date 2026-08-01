using CommunityToolkit.Mvvm.Messaging.Messages;
using NeoEditor.Data.Model;

namespace NeoEditor.Data.Messages;

// Q10=A: InitProfileMessage deleted (dead message).
public record EditProfileMessage(ProfileInfo ProfileInfo);

public record LoadProfileMessage(ProfileInfo ProfileInfo);

public record SaveProfileMessage(ProfileInfo ProfileInfo);