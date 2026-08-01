using System.ComponentModel.DataAnnotations;

namespace NeoEditor.Data.Model;

/// <summary>
///     攻击类型
/// </summary>
public enum AttackType
{
    [Display(Name = "近战攻击")] Melee = 0,

    [Display(Name = "远程攻击")] Ranged = 1
}

/// <summary>
///     武器声音分类
/// </summary>
public enum WeaponSound
{
    [Display(Name = "拳头/近战")] Punch = 0,

    [Display(Name = "爪子")] Claws,

    [Display(Name = "棍棒类")] Club,

    [Display(Name = "利刃")] Blade,

    [Display(Name = "长枪")] Rifle,

    [Display(Name = "短枪")] Pistol,

    [Display(Name = "激光")] Laser,

    [Display(Name = "弓箭类")] Bow,

    [Display(Name = "投掷")] Throw,

    [Display(Name = "勒死")] Choke,

    [Display(Name = "抓住")] Grasp,

    [Display(Name = "撕咬")] Bite
}

/// <summary>
///     状态颜色
/// </summary>
public enum ConditionColor
{
    [Display(Name = "白色")] White = 0,

    [Display(Name = "红色")] Red = 1,

    [Display(Name = "绿色")] Green = 2,

    [Display(Name = "黄色")] Yellow = 3
}

/// <summary>
///     剧情类型
/// </summary>
public enum EncounterType
{
    [Display(Name = "普通剧情")] Normal = 0,

    [Display(Name = "搜刮剧情")] Scavenge = 1
}

/// <summary>
///     攻击动作类型
/// </summary>
public enum BattleMoveType
{
    [Display(Name = "非攻击动作")] NonAttack = -1,

    [Display(Name = "近战攻击")] Melee = 0,

    [Display(Name = "远程攻击")] Ranged = 1
}

/// <summary>
///     地块是否可通行
/// </summary>
public enum PassableType
{
    [Display(Name = "不可通行")] Impassable = 0,

    [Display(Name = "可通行")] Passable = 1
}

/// <summary>
///     配方类型
/// </summary>
public enum RecipeType
{
    [Display(Name = "工具")] Tool = 0,

    [Display(Name = "食物")] Food,

    [Display(Name = "医务")] Medical,

    [Display(Name = "武器")] Weapon,

    [Display(Name = "载具")] Vehicle,

    [Display(Name = "杂项")] Misc
}