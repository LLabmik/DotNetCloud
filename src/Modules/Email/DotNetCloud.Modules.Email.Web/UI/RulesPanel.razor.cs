using DotNetCloud.Modules.Email.Web.Services;

namespace DotNetCloud.Modules.Email.Web.UI;

/// <summary>Code-behind for the rule list.</summary>
public partial class RulesPanel : EmailPanelBase
{
    private IReadOnlyList<EmailRuleDto> SortedRules => [.. State.Rules.OrderBy(rule => rule.Priority)];
}
