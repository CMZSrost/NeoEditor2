using NeoEditor.Data.Context;
using NeoEditor.Data.Models;

namespace NeoEditor.ViewModels.Controls.Tabs;

public class AttackModeViewModel(NeoContext db) : TabViewModel<attackmode>(db.attackmodes);