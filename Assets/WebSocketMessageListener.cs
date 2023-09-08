using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IWebSocketMessageListener
{
    void OnMessage(WebSocketMessageEvennia inMsg);
}
