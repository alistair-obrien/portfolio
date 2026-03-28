//using UnityEngine.Assertions;
//using Yarn.Unity;

//// Since we only plan to have one dialogue active at once, we could uses static vars here to modify behaviour
//// We are already kind of doing it
//// For example Parallel
//public class YarnBridge
//{
//    private static IGameController _gameController;
//    private static ScriptedActionQueue _currentScriptedQueue;

//    public static void Initialize(IGameController gameController)
//    {
//        _gameController = gameController;
//    }

//    [YarnCommand("MoveCharacterToPoint")]
//    public async static YarnTask MoveCharacterToPoint(string characterUid, string mapUid, int x, int y)
//    {
//        _gameController.QueueCharacterGameActionRequests(characterUid, _gameController.GetMoveCharacterToPointActionRequests(characterUid, mapUid, x, y));
//        await WaitForExternalScriptedQueues();
//    }

//    [YarnCommand("SetCharacterDialogue")]
//    public async static YarnTask SetCharacterDialogue(string characterId, string nodeTitle)
//    {
//        _gameController.QueueCharacterGameActionRequest(characterId, new SetCharacterDialogueRequest(characterId, nodeTitle));
//        await WaitForExternalScriptedQueues();
//    }

//    [YarnCommand("CreateItem")]
//    public async static YarnTask CreateItem(string characterId, string itemUid, string itemTemplateId)
//    {
//        _gameController.QueueCharacterGameActionRequest(characterId, 
//            new CreateItemFromTemplateRequest<IDataSource<Item>>(new ScriptableObjectAddressableLoader<IDataSource<Item>, Item>(itemTemplateId), itemUid));
//        await WaitForExternalScriptedQueues();
//    }

//    [YarnCommand("AddItemToCharacterInventory")]
//    public async static YarnTask AddItemToCharacterInventory(string characterId, string itemUid)
//    {
//        _gameController.QueueCharacterGameActionRequest(characterId, new MoveItemToCharacterInventoryRequest(characterId, itemUid));
//        await WaitForExternalScriptedQueues();
//    }

//    [YarnCommand("AddItemToCharacterInventoryAndShowPopup")]
//    public async static YarnTask AddItemToCharacterInventoryAndShowPopup(string characterId, string itemUid)
//    {
//        _gameController.QueueCharacterGameActionRequest(characterId, new AddItemToCharacterInventoryAndShowPopupRequest(characterId, itemUid));
//        await WaitForExternalScriptedQueues();
//    }

//    [YarnCommand("FadeToBlack")]
//    public async static YarnTask FadeToBlack(float seconds = -1)
//    {
//        _gameController.QueuePlayerGameActionRequest(new FadeToBlackRequest(seconds));
//        await WaitForExternalScriptedQueues();
//    }

//    [YarnCommand("FadeToGame")]
//    public async static YarnTask FadeToGame(float seconds = -1)
//    {
//        _gameController.QueuePlayerGameActionRequest(new FadeToGameRequest(seconds));
//        await WaitForExternalScriptedQueues();
//    }

//    [YarnCommand("Talk")]
//    public static void Talk(string selfId, string targetId, string dialogueNode)
//    {
//        _gameController.QueueCharacterGameActionRequest(selfId, new TalkRequest(selfId, targetId, dialogueNode));
//    }

//    [YarnCommand("OpenScriptedQueue")]
//    public static void OpenScriptedQueue()
//    {
//        _currentScriptedQueue = _gameController.OpenExternalScriptedActionQueue();
//    }

//    [YarnCommand("CloseScriptedQueue")]
//    public static async YarnTask CloseScriptedQueue()
//    {
//        Assert.IsNotNull(_currentScriptedQueue);

//        await WaitForExternalScriptedQueues();
//        _gameController.CloseExternalScriptedActionQueue(_currentScriptedQueue);
//        _currentScriptedQueue = null;
//    }

//    public static YarnTask WaitForExternalScriptedQueues()
//    {
//        return YarnTask.WaitUntil(() => _gameController.GetExternalActionQueuesEmpty());
//    }
//}
