using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class EvenniaTextConsole : MonoBehaviour, IWebSocketMessageListener
{

    public TMP_Text ConsoleText;

    public TMP_Text InputText;

    void Start() {
        WebSocketClient.Instance.RegisterMSGListener(this, "text");
    }

    public void OnMessage(WebSocketMessageEvennia inMsg)
    {
      if (ConsoleText != null && inMsg.GetCmd() == "text")
      {
            List<string> args = inMsg.GetArgs();
            ConsoleText.text = args[0];
      }
    }

    // for inputs...
    public void OnChangedInputField(string input)
    {
        Debug.Log("[OnChangedInputField] " + input);
    }

    public void OnEndedInputField(string input)
    {
        Debug.Log("[OnEndedInputField] " + input);

        // Send message to Evennia
  
        string jsonMsg = InputText.text;

        // Construct Text message.
        WebSocketMessageEvennia outMsg = WebSocketClient.NewMessage();

        // Set the command
        outMsg.SetCmd("text");

        // Set the args
        List<string> args = new List<string>();
        args.Add(InputText.text);
        outMsg.SetArgs(args);

        // No kwargs

        // Send message
        WebSocketClient.Instance.SendMessage(outMsg);

        // Clear input field
        InputText.text = "";
    }

    public void OnSelectedInputField(string input)
    {
        Debug.Log("[OnSelectedInputField] " + input);
    }

    public void OnDeslectedInputField(string input)
    {
        Debug.Log("[OnDeslectedInputField] " + input);
    }

}
