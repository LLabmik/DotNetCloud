using DotNetCloud.Modules.Email.Web.Services;

namespace DotNetCloud.Modules.Email.Web.UI;

/// <summary>Code-behind for the rule editor.</summary>
public partial class RuleEditorPanel : EmailPanelBase
{
    private void AddGroup()
    {
        if (State.Rule is not { } rule)
        {
            return;
        }

        var group = new RuleGroupDraft();
        group.Conditions.Add(new RuleConditionDraft());
        rule.Groups.Add(group);
    }
}
