using CommunityToolkit.Mvvm.Messaging.Messages;
using NeoEditor.Data.Model;

namespace NeoEditor.Data.Messages;

public record InitProfileMessage(string FilePath);

public record LoadProfileMessage(ProfileInfo ProfileInfo);
public record SaveProfileMessage(ProfileInfo ProfileInfo);
