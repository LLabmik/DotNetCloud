using DotNetCloud.Modules.Email.Models;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Email.UI;

/// <summary>Code-behind for the <see cref="EditRuleForm"/> component.</summary>
public partial class EditRuleForm : ComponentBase
{
    [Parameter, EditorRequired] public required EmailRule Rule { get; set; }
    [Parameter, EditorRequired] public required IReadOnlyList<EmailAccount> Accounts { get; set; }
    [Parameter] public Func<EmailRule, Task>? OnSave { get; set; }
    [Parameter] public Action? OnCancel { get; set; }

    private Guid? _selectedAccountId;

    protected override void OnParametersSet()
    {
        Rule.ConditionGroups ??= new List<EmailRuleConditionGroup>();
        Rule.Actions ??= new List<EmailRuleAction>();
    }

    private void AddConditionGroup()
    {
        Rule.ConditionGroups!.Add(new EmailRuleConditionGroup
        {
            Id = Guid.NewGuid(),
            MatchMode = ConditionMatchMode.All,
            Conditions = new List<EmailRuleCondition>
            {
                new() { Id = Guid.NewGuid(), Field = EmailRuleField.Subject, Operator = EmailRuleOperator.Contains, Value = "" }
            }
        });
    }

    private void AddAction()
    {
        Rule.Actions!.Add(new EmailRuleAction
        {
            Id = Guid.NewGuid(),
            ActionType = EmailRuleActionType.MarkRead
        });
    }

    private async Task HandleSave()
    {
        if (OnSave is not null)
            await OnSave.Invoke(Rule);
    }
}
