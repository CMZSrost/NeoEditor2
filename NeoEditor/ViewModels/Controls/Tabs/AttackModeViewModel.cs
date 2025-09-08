using NeoEditor.Data.Context;
using attackmode = NeoEditor.Data.Models.attackmode;

namespace NeoEditor.ViewModels.Controls.Tabs;

public class AttackModeViewModel(NeoContext db) : TabViewModel<attackmode>(db.attackmodes);