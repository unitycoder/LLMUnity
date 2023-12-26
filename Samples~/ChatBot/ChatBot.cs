using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;

public class ChatBot : MonoBehaviour
{
    public Transform chatContainer;
    public Color playerColor = new Color32(81, 164, 81, 255);
    public Color aiColor = new Color32(29, 29, 73, 255);
    public Color fontColor = Color.white;
    public Font font;
    public int fontSize = 16;
    public int bubbleWidth = 600;
    public LLMClient llm;
    public float textPadding = 10f;
    public float bubbleSpacing = 10f;
    public Sprite sprite;

    private InputBubble inputBubble;
    private List<Bubble> chatBubbles = new List<Bubble>();
    private bool blockInput = false;
    private BubbleUI playerUI, aiUI;
    private bool updatePositions=false;

    void Start()
    {
        if (font == null) font =  Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        playerUI = new BubbleUI {
            sprite=sprite,
            font=font,
            fontSize=fontSize,
            fontColor=fontColor,
            bubbleColor=playerColor,
            bottomPosition=0,
            leftPosition=0,
            textPadding=textPadding,
            bubbleOffset=bubbleSpacing,
            bubbleWidth=bubbleWidth,
            bubbleHeight=-1
        };
        aiUI = playerUI;
        aiUI.bubbleColor = aiColor;
        aiUI.leftPosition = 1;
        
        inputBubble = new InputBubble(chatContainer, playerUI, "InputBubble", "Message me", 4);
        inputBubble.AddSubmitListener(onInputFieldSubmit);
        inputBubble.AddValueChangedListener(onValueChanged);
        AllowInput();
    }

    void onInputFieldSubmit(string newText){
        inputBubble.ActivateInputField();
        if (blockInput || newText == "" || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)){
            StartCoroutine(BlockInteraction());
            return;
        }
        blockInput = true;
        // replace vertical_tab
        string message = inputBubble.GetText().Replace("\v", "\n");

        Bubble playerBubble = new Bubble(chatContainer, playerUI, "PlayerBubble", message);
        Bubble aiBubble = new Bubble(chatContainer, aiUI, "AIBubble", "...");
        chatBubbles.Add(playerBubble);
        chatBubbles.Add(aiBubble);
        SetUpdatePositions();

        BubbleTextSetter aiBubbleTextSetter = new BubbleTextSetter(this, aiBubble);
        Task chatTask = llm.Chat(message, aiBubbleTextSetter.SetText, AllowInput);

        inputBubble.SetText("");
    }
    
    public void AllowInput(){
        blockInput = false;
        inputBubble.ReActivateInputField();
    }

    IEnumerator<string> BlockInteraction()
    {
        // prevent from change until next frame
        inputBubble.setInteractable(false);
        yield return null;
        inputBubble.setInteractable(true);
        // change the caret position to the end of the text
        inputBubble.MoveTextEnd();
    }

    void onValueChanged(string newText){
        // Get rid of newline character added when we press enter
        if (Input.GetKey(KeyCode.Return)){
            if(inputBubble.GetText() == "\n")
                inputBubble.SetText("");
        }
    }

    public void SetUpdatePositions(){
        updatePositions = true;
    }

    public void UpdateBubblePositions()
    {
        float y = inputBubble.GetSize().y + inputBubble.GetRectTransform().offsetMin.y + bubbleSpacing;
        int lastBubbleOutsideFOV = -1;
        float containerHeight = chatContainer.GetComponent<RectTransform>().rect.height;
        for (int i = chatBubbles.Count - 1; i >= 0; i--) {
            Bubble bubble = chatBubbles[i];
            RectTransform childRect = bubble.GetRectTransform();
            childRect.position = new Vector2(childRect.position.x, y);

            // last bubble outside the container
            if (y > containerHeight && lastBubbleOutsideFOV == -1){
                lastBubbleOutsideFOV = i;
            }
            y += bubble.GetSize().y + bubbleSpacing;
        }
        // destroy bubbles outside the container
        for (int i = 0; i <= lastBubbleOutsideFOV; i++) {
            chatBubbles[i].Destroy();
        }
        chatBubbles.RemoveRange(0, lastBubbleOutsideFOV+1);
    }

    void Update()
    {
        if(!inputBubble.inputFocused())
        {
            inputBubble.ActivateInputField();
            StartCoroutine(BlockInteraction());
        }
        if(updatePositions){
            UpdateBubblePositions();
            updatePositions=false;
        }
    }
}