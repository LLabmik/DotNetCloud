#!/usr/bin/env python3
"""
Precise rename script for Tracks test files.
Handles partially-renamed files by checking what actually needs to change.
Only renames references that cause build errors (types, methods, DTOs, DbSets).
Does NOT rename test class names to avoid conflicts.
"""

import os

TEST_DIR = "/home/benk/Repos/dotnetcloud/tests/DotNetCloud.Modules.Tracks.Tests"

# Each tuple is (old_string, new_string).
# Order: longest/most specific first to avoid partial matches.
# Only includes mappings that cause BUILD ERRORS if not fixed.

REPLACEMENTS = [

    # ═══════════════════════════════════════════════════════════════
    # DTOs (longest compound names first)
    # ═══════════════════════════════════════════════════════════════

    ("CreateBoardFromTemplateDto", "CreateProductFromTemplateDto"),
    ("SaveBoardAsTemplateDto", "SaveProductAsTemplateDto"),
    ("CreateCardFromTemplateDto", "CreateItemFromTemplateDto"),
    ("SaveCardAsTemplateDto", "SaveItemAsTemplateDto"),
    ("StartReviewPokerDto", "CreatePokerSessionDto"),
    ("CreateBoardDto", "CreateProductDto"),
    ("UpdateBoardDto", "UpdateProductDto"),
    ("BoardListDto", "ProductListDto"),
    ("CreateCardDto", "CreateWorkItemDto"),
    ("UpdateCardDto", "UpdateWorkItemDto"),
    ("MoveCardDto", "MoveWorkItemDto"),
    ("BoardDto", "ProductDto"),
    ("CardDto", "WorkItemDto"),

    # ═══════════════════════════════════════════════════════════════
    # Enums
    # ═══════════════════════════════════════════════════════════════

    ("CardPriority.", "Priority."),
    ("CardPriority)", "Priority)"),  # typeof() etc.
    ("CardPriority?", "Priority?"),
    ("CardPriority ", "Priority "),
    ("BoardMode.", "ProductMode."),
    ("BoardMode?", "ProductMode?"),
    ("BoardMode ", "ProductMode "),
    ("BoardMode)", "ProductMode)"),
    ("CardDependencyType.", "DependencyType."),
    ("CardDependencyType?", "DependencyType?"),
    ("CardDependencyType ", "DependencyType "),
    ("CardDependencyType)", "DependencyType)"),

    # ═══════════════════════════════════════════════════════════════
    # Service class names (used as types, constructor calls)
    # ═══════════════════════════════════════════════════════════════

    # BoardService → ProductService
    ("new BoardService(", "new ProductService("),
    ("typeof(BoardService)", "typeof(ProductService)"),
    ("<BoardService>", "<ProductService>"),
    ("BoardService>", "ProductService>"),

    # CardService → WorkItemService
    ("new CardService(", "new WorkItemService("),
    ("typeof(CardService)", "typeof(WorkItemService)"),
    ("<CardService>", "<WorkItemService>"),
    ("CardService>", "WorkItemService>"),

    # BoardTemplateService → ProductTemplateService
    ("new BoardTemplateService(", "new ProductTemplateService("),
    ("typeof(BoardTemplateService)", "typeof(ProductTemplateService)"),
    ("<BoardTemplateService>", "<ProductTemplateService>"),

    # CardTemplateService → ItemTemplateService
    ("new CardTemplateService(", "new ItemTemplateService("),
    ("typeof(CardTemplateService)", "typeof(ItemTemplateService)"),
    ("<CardTemplateService>", "<ItemTemplateService>"),

    # LabelService → ProductService (for tests that create label-specific ProductService)
    ("new ProductService(_db);", "new ProductService(_db)"),  # keep as-is
    # Actually, if it's "new LabelService(" it's wrong. Replace.
    ("new LabelService(", "new ProductService("),

    # TeamService → TeamsController
    ("new TeamService(", "new TeamsController("),
    ("typeof(TeamService)", "typeof(TeamsController)"),
    ("<TeamService>", "<TeamsController>"),

    # ═══════════════════════════════════════════════════════════════
    # Controller class names
    # ═══════════════════════════════════════════════════════════════

    ("new BoardsController(", "new ProductsController("),
    ("typeof(BoardsController)", "typeof(ProductsController)"),
    ("new CardsController(", "new WorkItemsController("),
    ("typeof(CardsController)", "typeof(WorkItemsController)"),
    ("new BoardBacklogController(", "new SprintsController("),

    # ═══════════════════════════════════════════════════════════════
    # Service method names (the most common errors)
    # ═══════════════════════════════════════════════════════════════

    ("CreateBoardFromTemplateAsync", "CreateProductFromTemplateAsync"),
    ("SaveBoardAsTemplateAsync", "SaveProductAsTemplateAsync"),
    ("CreateCardFromTemplateAsync", "CreateItemFromTemplateAsync"),
    ("SaveCardAsTemplateAsync", "SaveItemAsTemplateAsync"),
    ("BroadcastReviewCardChangedAsync", "BroadcastReviewItemChangedAsync"),
    ("StartPokerForCurrentCardAsync", "StartSessionAsync"),
    ("SetCurrentCardAsync", "SetCurrentItemAsync"),
    ("SetCurrentCardRequest", "SetCurrentItemRequest"),
    ("SetReviewCurrentCard", "SetReviewCurrentItem"),
    ("GetReviewCardChanged", "GetReviewItemChanged"),
    ("CreateBoardAsync", "CreateProductAsync"),
    ("GetBoardAsync", "GetProductAsync"),
    ("ListBoardsAsync", "ListProductsByOrganizationAsync"),
    ("DeleteBoardAsync", "DeleteProductAsync"),
    ("UpdateBoardAsync", "UpdateProductAsync"),
    ("HardDeleteBoardAsync", "HardDeleteProductAsync"),
    ("UndeleteBoardAsync", "UndeleteProductAsync"),
    ("CreateCardAsync", "CreateWorkItemAsync"),
    ("GetCardAsync", "GetWorkItemAsync"),
    ("ListCardsAsync", "GetWorkItemsBySwimlaneAsync"),
    ("MoveCardAsync", "MoveWorkItemAsync"),
    ("DeleteCardAsync", "DeleteWorkItemAsync"),
    ("UpdateCardAsync", "UpdateWorkItemAsync"),
    ("AddLabelToCardAsync", "AddLabelAsync"),
    ("RemoveLabelFromCardAsync", "RemoveLabelAsync"),
    ("GetBacklogCardsAsync", "GetBacklogItemsAsync"),
    ("AddCardToSprintAsync", "AddItemToSprintAsync"),
    ("RemoveCardFromSprintAsync", "RemoveItemFromSprintAsync"),
    ("GetCardByNumberAsync", "GetWorkItemByNumberAsync"),
    ("GetChildCardsAsync", "GetChildWorkItemsAsync"),
    ("GetCardsBySwimlaneAsync", "GetWorkItemsBySwimlaneAsync"),
    ("ExportCardsCsvAsync", "ExportWorkItemsCsvAsync"),
    ("ImportCardsCsvAsync", "ImportWorkItemsCsvAsync"),
    ("BulkCardActionAsync", "BulkWorkItemActionAsync"),
    ("GetBoardTitleAsync", "GetProductTitleAsync"),
    ("GetBoardTitlesAsync", "GetProductTitlesAsync"),
    ("GetCardTitleAsync", "GetWorkItemTitleAsync"),
    ("GetCardTitlesAsync", "GetWorkItemTitlesAsync"),
    ("SearchBoardsAsync", "SearchProductsAsync"),
    ("SearchCardsAsync", "SearchWorkItemsAsync"),
    ("GetBoardAnalyticsAsync", "GetProductAnalyticsAsync"),
    ("GetBoardDashboardAsync", "GetProductDashboardAsync"),
    ("GetBoardCapacityAsync", "GetProductCapacityAsync"),
    ("GetActivitiesByBoardAsync", "GetActivitiesByProductAsync"),
    ("GetActivitiesByCardAsync", "GetActivitiesByWorkItemAsync"),
    ("SeedBoardAsync", "SeedProductAsync"),
    ("SeedCardAsync", "SeedWorkItemAsync"),
    ("GetBoardUserRoleAsync", "GetUserProductRoleAsync"),
    ("BroadcastBoardActionAsync", "BroadcastProductActionAsync"),
    ("BroadcastCardActionAsync", "BroadcastWorkItemActionAsync"),
    ("AddUserToBoardGroupAsync", "AddUserToProductGroupAsync"),
    ("RemoveUserFromBoardGroupAsync", "RemoveUserFromProductGroupAsync"),

    # SprintPlanningService methods
    ("CreateYearPlanAsync", "CreateSprintPlanAsync"),
    ("GetYearPlanAsync", "GetSprintPlanAsync"),

    # CreateSprintPlanDto properties
    (".SprintCount", ".NumberOfSprints"),
    (".DefaultDurationWeeks", ".SprintDurationWeeks"),

    # ═══════════════════════════════════════════════════════════════
    # DbSet property names (on _db / db / DbContext)
    # ═══════════════════════════════════════════════════════════════

    (".Boards.", ".Products."),
    (".Boards;", ".Products;"),
    ("db.Boards", "db.Products"),
    ("_db.Boards", "_db.Products"),
    ("DbContext.Boards", "DbContext.Products"),

    (".Cards.", ".WorkItems."),
    ("db.Cards", "db.WorkItems"),
    ("_db.Cards", "_db.WorkItems"),

    (".BoardMembers.", ".ProductMembers."),
    ("db.BoardMembers", "db.ProductMembers"),
    ("_db.BoardMembers", "_db.ProductMembers"),

    (".BoardSwimlanes.", ".Swimlanes."),
    ("db.BoardSwimlanes", "db.Swimlanes"),
    ("_db.BoardSwimlanes", "_db.Swimlanes"),

    (".CardAssignments.", ".WorkItemAssignments."),
    ("db.CardAssignments", "db.WorkItemAssignments"),
    ("_db.CardAssignments", "_db.WorkItemAssignments"),

    (".CardLabels.", ".WorkItemLabels."),
    ("db.CardLabels", "db.WorkItemLabels"),
    ("_db.CardLabels", "_db.WorkItemLabels"),

    (".CardComments.", ".WorkItemComments."),
    ("db.CardComments", "db.WorkItemComments"),
    ("_db.CardComments", "_db.WorkItemComments"),

    (".CardAttachments.", ".WorkItemAttachments."),
    ("db.CardAttachments", "db.WorkItemAttachments"),
    ("_db.CardAttachments", "_db.WorkItemAttachments"),

    (".CardDependencies.", ".WorkItemDependencies."),
    ("db.CardDependencies", "db.WorkItemDependencies"),
    ("_db.CardDependencies", "_db.WorkItemDependencies"),

    (".CardWatchers.", ".WorkItemWatchers."),
    ("db.CardWatchers", "db.WorkItemWatchers"),
    ("_db.CardWatchers", "_db.WorkItemWatchers"),

    (".CardTemplates.", ".ItemTemplates."),
    ("db.CardTemplates", "db.ItemTemplates"),
    ("_db.CardTemplates", "_db.ItemTemplates"),

    (".BoardTemplates.", ".ProductTemplates."),
    ("db.BoardTemplates", "db.ProductTemplates"),
    ("_db.BoardTemplates", "_db.ProductTemplates"),

    (".SprintCards.", ".SprintItems."),
    ("db.SprintCards", "db.SprintItems"),
    ("_db.SprintCards", "_db.SprintItems"),

    (".CardFieldValues.", ".WorkItemFieldValues."),
    (".CardShareLinks.", ".WorkItemShareLinks."),

    # ═══════════════════════════════════════════════════════════════
    # Entity types (used as variable types, generics, typeof)
    # ═══════════════════════════════════════════════════════════════

    # Core entity types (used as bare type names in field/variable declarations)
    # Must be followed by space, ?, >, ), ,, ;, [, or at end of string
    # so we don't match longer identifiers
    ("Board ", "Product "),
    ("Board?", "Product?"),
    ("Board>", "Product>"),
    ("Board)", "Product)"),
    ("Board,", "Product,"),
    ("Board[", "Product["),
    ("(Board)", "(Product)"),
    ("typeof(Board)", "typeof(Product)"),

    ("Card ", "WorkItem "),
    ("Card?", "WorkItem?"),
    ("Card>", "WorkItem>"),
    ("Card)", "WorkItem)"),
    ("Card,", "WorkItem,"),
    ("Card[", "WorkItem["),
    ("(Card)", "(WorkItem)"),
    ("typeof(Card)", "typeof(WorkItem)"),

    ("BoardSwimlane ", "Swimlane "),
    ("BoardSwimlane?", "Swimlane?"),
    ("BoardSwimlane>", "Swimlane>"),
    ("BoardSwimlane)", "Swimlane)"),
    ("BoardSwimlane,", "Swimlane,"),
    ("BoardSwimlane;", "Swimlane;"),
    ("(BoardSwimlane)", "(Swimlane)"),

    # Service/controller types as bare type references
    ("BoardService ", "ProductService "),
    ("BoardService?", "ProductService?"),
    ("BoardService>", "ProductService>"),
    ("BoardService)", "ProductService)"),
    ("BoardService,", "ProductService,"),

    ("CardService ", "WorkItemService "),
    ("CardService?", "WorkItemService?"),
    ("CardService>", "WorkItemService>"),
    ("CardService)", "WorkItemService)"),

    ("BoardTemplateService ", "ProductTemplateService "),
    ("BoardTemplateService?", "ProductTemplateService?"),
    ("BoardTemplateService>", "ProductTemplateService>"),

    ("CardTemplateService ", "ItemTemplateService "),
    ("CardTemplateService?", "ItemTemplateService?"),
    ("CardTemplateService>", "ItemTemplateService>"),

    ("BoardsController ", "ProductsController "),
    ("BoardsController?", "ProductsController?"),
    ("BoardsController>", "ProductsController>"),
    ("BoardsController)", "ProductsController)"),

    ("CardsController ", "WorkItemsController "),
    ("CardsController?", "WorkItemsController?"),
    ("CardsController>", "WorkItemsController>"),
    ("CardsController)", "WorkItemsController)"),

    ("LabelService ", "ProductService "),

    ("TeamService ", "TeamsController "),
    ("TeamService?", "TeamsController?"),
    ("TeamService>", "TeamsController>"),

    ("BulkOperationService ", "WorkItemsController "),
    ("SprintReportService ", "AnalyticsService "),

    # SprintCard → SprintItem (entity)
    ("SprintCard>", "SprintItem>"),
    ("SprintCard?", "SprintItem?"),
    ("SprintCard ", "SprintItem "),
    ("SprintCard)", "SprintItem)"),
    ("SprintCard,", "SprintItem,"),
    ("SprintCard;", "SprintItem;"),
    ("(SprintCard", "(SprintItem"),

    # CardSummary → WorkItemSummary (from ITracksDirectory)
    ("CardSummary>", "WorkItemSummary>"),
    ("CardSummary?", "WorkItemSummary?"),
    ("CardSummary ", "WorkItemSummary "),
    ("CardSummary)", "WorkItemSummary)"),
    ("CardSummary,", "WorkItemSummary,"),
    ("<CardSummary>", "<WorkItemSummary>"),

    # ═══════════════════════════════════════════════════════════════
    # Common property names
    # ═══════════════════════════════════════════════════════════════

    (".BoardId", ".ProductId"),
    (".CardId", ".WorkItemId"),
    (".BoardTitle", ".ProductTitle"),
    (".CardTitle", ".WorkItemTitle"),
    (".CardCount", ".ItemCount"),
    (".CurrentCardId", ".CurrentWorkItemId"),
    (".CurrentCard", ".CurrentWorkItem"),
    ("CurrentCardId", "CurrentWorkItemId"),
    ("CurrentCard", "CurrentWorkItem"),
    ("CardCount", "ItemCount"),
    ("BoardCount", "ProductCount"),

    # ═══════════════════════════════════════════════════════════════
    # Field / variable names (member variables in tests)
    # ═══════════════════════════════════════════════════════════════

    ("ProductService _boardService", "ProductService _productService"),
    ("WorkItemService _cardService", "WorkItemService _workItemService"),
    ("ProductService _labelService", "ProductService _labelService"),  # keep label semantic
    ("TeamsController _teamService", "TeamsController _teamController"),
    # But _boardService might be used as a field name in methods too
    ("_boardService.", "_productService."),
    ("_cardService.", "_workItemService."),
    ("_boardTemplateService.", "_productTemplateService."),
    ("_cardTemplateService.", "_itemTemplateService."),
    ("_boardController.", "_productController."),
    ("_cardController.", "_workItemController."),

    # Variable names for entity instances
    ("Product _board ", "Product _product "),
    ("Product _board;", "Product _product;"),
    ("Product _board =", "Product _product ="),
    ("Product _board,", "Product _product,"),
    ("var _board =", "var _product ="),
    ("var _board;", "var _product;"),
    ("WorkItem _card", "WorkItem _workItem"),
    ("var _card =", "var _workItem ="),
    ("var _card;", "var _workItem;"),
    ("Product? _board", "Product? _product"),
    ("WorkItem? _card", "WorkItem? _workItem"),

    # ═══════════════════════════════════════════════════════════════
    # Route/URL strings
    # ═══════════════════════════════════════════════════════════════

    ('"api/v1/boards/', '"api/v1/products/'),
    ('"api/v1/boards"', '"api/v1/products"'),
    ('"api/v1/cards/', '"api/v1/workitems/'),
    ('"api/v1/cards"', '"api/v1/workitems"'),
    ('/boards/', '/products/'),
    ('/cards/', '/workitems/'),
    ('board_id', 'product_id'),
    ('card_id', 'work_item_id'),
]


def fix_file(filepath):
    """Apply replacements. Returns number of changes."""
    with open(filepath, 'r', encoding='utf-8') as f:
        content = f.read()

    original = content
    changes = 0

    for old, new in REPLACEMENTS:
        count = content.count(old)
        if count > 0:
            content = content.replace(old, new)
            changes += count

    if content != original:
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write(content)

    return changes


def main():
    total = 0
    for fn in sorted(os.listdir(TEST_DIR)):
        if not fn.endswith('.cs'):
            continue
        fp = os.path.join(TEST_DIR, fn)
        n = fix_file(fp)
        if n:
            print(f"  {fn}: {n}")
            total += n
    print(f"\nTotal replacements: {total}")


if __name__ == '__main__':
    main()
