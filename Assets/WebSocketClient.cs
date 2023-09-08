using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NativeWebSocket;
using Newtonsoft.Json;
using System.Text;

using Newtonsoft.Json.Linq;

[System.Serializable]
// A class representing a websocket MSG from Evennia
// All data coming into the webclient is in the form of valid JSON on the form
// 
// `["outputfunc_name", [args], {kwarg}]`
// 
// which represents a "outputfunc" or "inputfunc" command with *args, **kwargs (a list, and a dictionary of variable data)
// The most common inputfunc is "text", which takes just the text input
// from the command line and interprets it as an Evennia Command: `["text", ["look"], {}]`
// For all inputfuncs, see https://www.evennia.com/docs/latest/api/evennia.server.inputfuncs.html?highlight=outputfunc
public class WebsocketMessageEvenniaJSON : WebSocketMessageEvennia
{
  public string cmd;

  public List<string> args;

  // Another JSON object?
  // We use JToken here because it's unsure what kwargs will be.
  // It could be "options" : {} ,  (Another JObject , i.e. a dictionary)
  // It could be "channel" : 1,    (A JValue, i.e an int)
  // So we have to check, depending on cmd/args, what it is later.
  // On how JToken could be used: https://stackoverflow.com/questions/21246609/deserializing-an-unknown-type-in-json-net
  public Dictionary<string, JToken> kwargs;

  // Get a typed kwarg (must know it's type)
  override public T GetKwarg<T>(string key) {
    JToken tok = kwargs[key];
    JValue val = tok as JValue;

    return val.ToObject<T>();
  }

  override public string GetCmd()
  {
    return cmd;
  }

  override public void SetCmd(string inCmd)
  {
    cmd = inCmd;
  }

  override public List<string> GetArgs()
  {
    return args;
  }
  override public void SetArgs(List<string> inArgs)
  {
    args = inArgs;
  }

  override public int GetNumKwarg()
  {
    if (kwargs == null) {
      return 0;
    }

    return kwargs.Count;
  }

  override public void SetKwarg<T>(string key, T value) {
    JToken tok = JToken.FromObject(value);

    if (kwargs == null) {
      kwargs = new Dictionary<string, JToken>();
    }
    kwargs[key] = tok;
  }

  override public bool HasKwarg(string key) {
    if (kwargs == null) {
      return false;
    }
  
    return kwargs.ContainsKey(key);
  }

  public static string SerializeJSON(WebsocketMessageEvenniaJSON inMsg)
  {
    string retStr = JsonConvert.SerializeObject(inMsg);

    return retStr;
  }

  public static WebsocketMessageEvenniaJSON CreateFromJSON(string jsonString)
  {
    return JsonConvert.DeserializeObject<WebsocketMessageEvenniaJSON>(jsonString);
  }

  public static T GetValueJToken<T>(JToken tok) {
    return tok.ToObject<T>();
  }

  public WebsocketMessageEvenniaJSON() {
    cmd = "";
    args = new List<string>();
    kwargs = new Dictionary<string, JToken>(); // empty
  }
}

// Server options.  The process is:
//  Client-server connect
//  Client queries server configs (via 'hello' inputfunc)
//  Client sends connection options
// Others can be found at https://www.evennia.com/docs/latest/api/evennia.server.inputfuncs.html?highlight=outputfunc

[System.Serializable]
public class ClientServerOptions {
  // Key: ENCODING
  // Val: string
  public string Encoding = "utf-8";
  
  // Key: nocolor
  // Val: bool
  public bool NoColor = false;

  // Key: utf-8
  // Val: bool
  public bool UTF8 = true;

  // Key: inputdebug
  // Val: bool
  public bool InputDebug = false;

  public void HandleMsg_ClientOptions(WebsocketMessageEvenniaJSON msg)
  {
    // Look at the kwargs , they should contain {string : vals}
    string _encoding = msg.GetKwarg<string>("ENCODING");
    Encoding = _encoding;

    bool _noColor = msg.GetKwarg<bool>("NOCOLOR");
    NoColor = _noColor;

    bool utf8 = msg.GetKwarg<bool>("UTF-8");
    UTF8 = utf8;

    bool _inputDebug = msg.GetKwarg<bool>("INPUTDEBUG");
    InputDebug = _inputDebug;
  }
}

public class WebSocketClient : MonoBehaviour
{
  public static WebSocketClient Instance;

  // IP to connect to.
  // Use localHost if needed.
  public string IpAddr = "localhost";
  // Port to connect to.
  public string Port = "4008";

  // Is the actual websocket connection established?
  public bool IsConnected { get { return (websocket.State == WebSocketState.Open); } }
  public bool IsConnecting { get { return (websocket.State == WebSocketState.Connecting); } }

  // Have we done other non-socket initialization post-connection? 
  public bool IsInited = false;

  public ClientServerOptions ConnectionOptions = new ClientServerOptions();

  WebSocket websocket;

  List<MessageListenerPair> MessageListeners = new List<MessageListenerPair>();

  public void Awake() {
    if (Instance == null) {
      Instance = this;
    } else {
      Destroy(Instance);
    }
  }

  // Start is called before the first frame update
  void Start()
  {
    websocket = new WebSocket("ws://" + IpAddr + ":" + Port);

    websocket.OnOpen += () =>
    {
      Debug.Log("Connection open!");
    };

    websocket.OnError += (e) =>
    {
      Debug.Log("Error! " + e);
    };

    websocket.OnClose += (e) =>
    {
      Debug.Log("Connection closed!");
    };

    websocket.OnMessage += (bytes) =>
    {
      Debug.Log("OnMessage!");
      Debug.Log(bytes);

      // getting the message as a string
       var message = System.Text.Encoding.UTF8.GetString(bytes);

      Debug.Log("OnMessage! " + message);
    
      WebsocketMessageEvenniaJSON msg = WebsocketMessageEvenniaJSON.CreateFromJSON(message);

      if (msg.cmd == "client_options")
      {
        // We recieved a client_options message.

        ConnectionOptions.HandleMsg_ClientOptions(msg);
      }
    
      for (int i = 0; i < MessageListeners.Count; ++i) {
        if (MessageListeners[i].MsgType == msg.cmd) {
          MessageListeners[i].Listener.OnMessage(msg);
        }
      }

    };
  
    // We connect in Update
  }

  public void SendMessage(WebSocketMessageEvennia outgoingMsg) {
    WebsocketMessageEvenniaJSON outgoingMsgJSON = outgoingMsg as WebsocketMessageEvenniaJSON;
  
    string jsonString = WebsocketMessageEvenniaJSON.SerializeJSON(outgoingMsgJSON);
    Debug.Log("Parsed JSON Message: \"" + jsonString + "\"");
    SendWebsocketMessage( jsonString );
  }

  // A function to send a websocket message in the form of a JSON string.
  async void SendWebsocketMessage(string jsonMsg)
  {
    if (IsConnected)
    {
      // TODO: Make these part of the default getter/setter for these fields.
      byte[] bytes = Encoding.Default.GetBytes(jsonMsg);
      string retStr = Encoding.UTF8.GetString(bytes);
      retStr = retStr.Replace("\u200B", "");

      Debug.Log("Sending message: " + retStr);
      await websocket.SendText(retStr);
    } else {
      Debug.LogError("Can't send message because not connected!");
    }
  }


  async void RequestClientServerSettings()
  {
    if (IsConnected) {
      // Construct a 'hello' message with 'get' and just fill in the settings for now.
      // Evennia should send us back a 'client_options' msg.
      // TODO: In the future, maybe actually send other options in.
      WebsocketMessageEvenniaJSON pm = new WebsocketMessageEvenniaJSON();
      pm.cmd = "hello";
      pm.args = new List<string>(); // empty
      pm.SetKwarg("get", true);
    
      string jsonString = WebsocketMessageEvenniaJSON.SerializeJSON(pm);
      SendWebsocketMessage(jsonString);
  
      // ... when we get a client_options message, we will update the settings.
    } else {
      Debug.Log("Not connected yet...");
    }
  }

  void InitPostConnection() {
      // Request initial client/server options.
      RequestClientServerSettings();
  }

  async void Update()
  {
    #if !UNITY_WEBGL || UNITY_EDITOR
      websocket.DispatchMessageQueue();
#endif


    // we are currently trying to connect and we aren't connected yet.
    if (IsConnecting == false && IsConnected == false)
    {
      // Keep sending messages at every 0.3s
      //InvokeRepeating("SendWebSocketMessage", 0.0f, 0.3f);

      // waiting for messages
      await websocket.Connect();
    } else if (IsConnected && IsInited == false)
    {
      InitPostConnection();
      IsInited = true;
    }
  }

  private async void OnApplicationQuit()
  {
    await websocket.Close();
  }

  // Static interface functions

  public struct MessageListenerPair {
    public IWebSocketMessageListener Listener;
    public string MsgType;
  }


  // Registers a listener to listen for messages.
  public void RegisterMSGListener(IWebSocketMessageListener listener, string msgType) {
    for (int i = 0; i < MessageListeners.Count; ++i) {
      if ((MessageListeners[i].Listener == listener) && (MessageListeners[i].MsgType == msgType)) {
        return; // already listening for this type of message.
      }
    }


    MessageListenerPair newListener;
    newListener.Listener = listener;
    newListener.MsgType = msgType;

    MessageListeners.Add(newListener);
  }

  public static WebSocketMessageEvennia NewMessage()
  {
    WebsocketMessageEvenniaJSON socketMsg = new WebsocketMessageEvenniaJSON();
    return socketMsg;
  }

}