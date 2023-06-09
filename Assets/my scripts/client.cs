using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using Steamworks;
using System.Linq;
using Steamworks.Data;

public class client : MonoBehaviour
{
    bool connected;
    public static int myPlayerNum;
    public LinkedList<HitPacket> unConfirmed;
    public LinkedList<byte[]> Queue = new LinkedList<byte[]>();
    public bool playing;
    public GameObject FpsController;
    public static Transform cam;
    public GameObject EnemyPrefab;
    public ConnectionPacket lastConnectionPacket;//all statics except for instance are to be set outside the script
    public static client instance;
    public IPEndPoint localEp;
    public static SteamId server;//needs to be set outside of this script before scene load
    public static string username;//needs to be set outside of this script before scene load
    //public Socket socket;
    public List<Player> Players = new List<Player>();
    bool lastconnvalue;
    
    public static byte commandToByte(string command) 
    {
        switch (command)
        {
            case "switchWeapon":
                return 1;
            case "noShoot":
                return 2;
            case "pickupWeapon":
                return 3;
            case "reload":
                return 4;
            case "aim_in":
                return 5;
            case "aim_out":
                return 6;
            case "change_gear":
                return 7;
            default:
                return 0;
                
        }
    }
    public static IPAddress GetLocalIPAddress()
    {
        if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip;
                }
            }
            return new IPAddress(new byte[] { 127, 0, 0, 1 });
        }
        return new IPAddress(new byte[] { 127, 0, 0, 1 });
    }
    // Start is called before the first frame update
    void Awake()
    {
        SteamNetworkingUtils.SendBufferSize = 512;
        SteamNetworkingUtils.InitRelayNetworkAccess();
        lastConnectionPacket = new ConnectionPacket();
       
       
            SteamNetworking.AllowP2PPacketRelay(true);
            
           
            username = SteamManager.Instance.PlayerName;
            Debug.Log(username);
       
        
        server = SteamClient.SteamId;
        if (File.Exists("host.txt"))
        {
            StreamReader reader = new StreamReader("host.txt");
            ulong num = ulong.Parse(reader.ReadLine());
            Debug.Log("joining " + new Lobby(num).Owner);
            SteamMatchmaking.JoinLobbyAsync(num);
        }
        else {
            PacketHandler.instance.gameObject.AddComponent<Server>();
            PacketHandler.instance.gameObject.GetComponent<Server>().EnemyPrefab = EnemyPrefab;
            PacketHandler.instance.gameObject.GetComponent<Server>().init();
        }
        if (instance != null && instance != this)
        {
            gameObject.SetActive(false);
        }
        else
        {
            unConfirmed = new LinkedList<HitPacket>();
            //socket = new Socket(AddressFamily.InterNetwork,SocketType.Dgram, ProtocolType.Udp);
            playing = true;
            instance = this;
            //socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            StartCoroutine(ConnectionTick());
            StartCoroutine(MovementTick());
            
        }
    }
    
    private void OnDestroy()
    {
        playing = false;
    }
    private void OnApplicationQuit()
    {
        playing = false;
    }
    public void clientHandlePacket(Packet p)
    {
        
        if (p is MovementPacket)
        {

        }
        //if its a connection packet log the interaction
        else if (p is ConnectionPacket)
        {
            using (ConnectionPacket P = (ConnectionPacket)p)
            {
                

                myPlayerNum = P.playernum;
                
                if (p.playernum != myPlayerNum && Players.Count == p.playernum)
                {
                    PacketHandler.makeNewPlayerClient(P);
                    Debug.Log("client got connection packet from " + p.playernum + " my num " + myPlayerNum);
                }
                else if (p.playernum == myPlayerNum)
                {
                    //Debug.Log("client got connection packet from self");
                }
                else 
                {
                    Debug.Log("client got connection packet from " + p.playernum + " my num " + myPlayerNum);
                }
                lastConnectionPacket.ticks = p.ticks;
                lastConnectionPacket.playernum = p.playernum;
                lastConnectionPacket.username = P.username;
                lastConnectionPacket.usernames = P.usernames;
                myPlayerNum = P.playernum;
                connected = true;
            }
           
        }//if its a hit acknowledgement we send it back after removing the correct node from unconfirmed
        else if (p is HitAck)
        {
            HitAck P = (HitAck)p;
            LinkedListNode<HitPacket> unconfirmed = client.instance.unConfirmed.First;
            int j = client.instance.unConfirmed.Count;
            for (int i = 0; i < j; i++)
            {
                if (unconfirmed.Value.id == P.id)
                {

                    unconfirmed = unconfirmed.Next;
                    client.instance.unConfirmed.Remove(unconfirmed.Previous);
                    //hitmarker happens here
                    break;
                }
                else
                {
                    unconfirmed = unconfirmed.Next;
                }
            }
            SteamNetworking.SendP2PPacket(client.server, PacketHandler.toServer(P.toBytes()), (int)PacketHandler.toServer(P.toBytes()).Length, 0,P2PSend.UnreliableNoDelay);
            
        }
    }
    public void clientHandlePacket(ServerFragment s)
    {
        Player p = new Player();
        bool contained = false;
        foreach (var item in Players)
        {
            if (item.playernum == s.playernum)
            {
                p = item;
                contained = true;
            }
        }
        if (!contained) 
        {
           p = PacketHandler.makeNewPlayerClient(s);
        }
        
        MovementPacket m = new MovementPacket(s.position, s.Rotation, s.playernum);//movement packet of the enemy
        m.timeCreated = s.timeCreated;//make sure they are identical
        LinkedListNode<Packet> node;
        PacketHandler.placeInOrderUnique(p.PacketHistory, m, out node);
        
        
        Enemy enemy = p.Dummy.GetComponent<Enemy>();//adjust the Enemy component
        enemy.username = p.userName;
        enemy.health = 100 - s.damageTaken;
        enemy.playernum = s.playernum;
    }
    IEnumerator ConnectionTick() 
    {
        while (playing) 
        {
            //Debug.Log(server);
            //Debug.Log(username);
            //Debug.Log(myPlayerNum);
            
            try
            {
                SteamNetworking.SendP2PPacket(server, PacketHandler.toServer((new ConnectionPacket(username, lastConnectionPacket.playernum)).toBytes()), (int)PacketHandler.toServer((new ConnectionPacket(username, lastConnectionPacket.playernum)).toBytes()).Length, 0, P2PSend.UnreliableNoDelay);
            }
            catch (Exception E)
            {

                Debug.Log(E.Message);
            }
            
            
            
            yield return new WaitForSecondsRealtime(1);
        }    
    }
    IEnumerator MovementTick()
    {
        while (playing) 
        {
            if (connected)
            {
                
                    MovementPacket movement = new MovementPacket(FpsController.transform.position, FpsController.transform.rotation, myPlayerNum);//construct a movement packet out of our player
                    LinkedListNode<HitPacket> shot = unConfirmed.Last;

                    for (int i = 0; i < unConfirmed.Count; i++)//send all unconfirmed shots
                    {
                        SteamNetworking.SendP2PPacket(client.server, PacketHandler.toServer(shot.Value.toBytes()), (int)PacketHandler.toServer(shot.Value.toBytes()).Length, 0, P2PSend.UnreliableNoDelay);


                        if (shot.Previous == null)
                            break;
                        shot = shot.Previous;
                    }
                    SteamNetworking.SendP2PPacket(client.server, PacketHandler.toServer(movement.toBytes()), (int)PacketHandler.toServer(movement.toBytes()).Length, 0, P2PSend.UnreliableNoDelay);
                
                
            }    
            yield return new WaitForSecondsRealtime(1f/128f);
        }
    }
   
        
    
      

    
    private void FixedUpdate()
    {
        if (!lastconnvalue && connected) 
        {
            
            Debug.Log("connected with playernum " + myPlayerNum);
        }
        lastconnvalue = connected;
        LinkedListNode<byte[]> buff = instance.Queue.Last;//process them fifo
        int count = 0;
        if (buff != null)
        {
            count = instance.Queue.Count;//prevent modifying changing elements
        }
        for (int i = 0; i < count; i++)
        {
            
            try
            {
                BinaryFormatter b = new BinaryFormatter();
                MemoryStream m = new MemoryStream(buff.Value);
                var pack_ = b.Deserialize(m);
                if (pack_ is Packet)
                {
                    clientHandlePacket((Packet)pack_);
                }
                else if (pack_ is ServerFragment)
                {

                    clientHandlePacket((ServerFragment)pack_);
                }
            }
            catch (Exception)
            {

                
            }
            
            
           
            //integrate the buffer and endpoint down the queue
            buff = buff.Previous;
            instance.Queue.Last.Value = null;

            instance.Queue.RemoveLast();
        }
        if (Input.GetMouseButtonDown(0)&&client.cam != null) 
        {
            RaycastHit hit;
            if (Physics.Raycast(cam.position, cam.forward, out hit))
            { 
                if (hit.distance <= 50) 
                {
                    //splatter effect
                    if (hit.transform.gameObject.GetComponent<Enemy>()!=null) 
                    {
                        Enemy Shot = hit.transform.gameObject.GetComponent<Enemy>();
                        HitPacket h = new HitPacket(cam, instance.gameObject.transform, new int[] { Shot.playernum });
                        unConfirmed.AddFirst(h);
                    }
                }
            }

        }
    }
    // Update is called once per frame
    void Update()
    {
        foreach (Player player in Players.ToArray())
        {
            if (player.playernum != myPlayerNum)//(player.playernum != lastConnectionPacket.playernum)
            {
                if (player.PacketHistory.Count > 2)
                {
                    // this graph explains this block https://gyazo.com/f242ed66b95169f5cf85347ccee5f671
                    Vector3 n_nocross = (((MovementPacket)player.PacketHistory.First.Value).position - ((MovementPacket)player.PacketHistory.First.Next.Value).position);
                    Vector3 n = Vector3.Cross(n_nocross, Vector3.up);
                    Vector3 i = (((MovementPacket)player.PacketHistory.First.Value).position - ((MovementPacket)player.PacketHistory.First.Next.Next.Value).position);
                    Vector3 j = (((MovementPacket)player.PacketHistory.First.Next.Value).position - ((MovementPacket)player.PacketHistory.First.Next.Next.Value).position);
                    Vector3 PredictionPoint = (i - 2 * n * Vector3.Dot(i, n));
                    //float avgSpeed = (n_nocross.magnitude + j.magnitude) / (float)(((MovementPacket)player.PacketHistory.First.Value).timeCreated - ((MovementPacket)player.PacketHistory.First.Next.Next.Value).timeCreated).TotalSeconds;//place the enemy players with magic
                    float speed = i.magnitude/ (float)((player.PacketHistory.First.Value.timeCreated- player.PacketHistory.First.Next.Next.Value.timeCreated).TotalMilliseconds/1000.0);


                    float timeCoeff = (float)((DateTime.Now - player.PacketHistory.First.Value.timeCreated).TotalMilliseconds / (player.PacketHistory.First.Next.Value.timeCreated - player.PacketHistory.First.Next.Next.Value.timeCreated).TotalMilliseconds);
                    //print(((float)(DateTime.Now).Millisecond) - (player.PacketHistory.First.Value.timeCreated.Millisecond) + " " + ((player.PacketHistory.First.Value.timeCreated.Millisecond) - (player.PacketHistory.First.Next.Next.Value.timeCreated.Millisecond)));
                    
                    if (j.magnitude == 0) 
                    {
                        speed = n_nocross.magnitude;
                    }
                    speed = Mathf.Clamp(speed, 0, 100);
                    timeCoeff = Mathf.Clamp(timeCoeff,0f,1f);
                    Debug.DrawLine(((MovementPacket)player.PacketHistory.First.Value).position, ((MovementPacket)player.PacketHistory.First.Value).position + (PredictionPoint));
                    Vector3 playerposition;
                    if (speed<PredictionPoint.magnitude*1.2f)
                        playerposition = ((MovementPacket)player.PacketHistory.First.Value).position + (PredictionPoint.normalized*speed);
                    else
                        playerposition = ((MovementPacket)player.PacketHistory.First.Value).position + (PredictionPoint * timeCoeff);

                    player.Dummy.transform.position = playerposition;
                    
                    player.Dummy.transform.rotation = ((MovementPacket)player.PacketHistory.First.Value).lookrotation * Quaternion.Euler((((MovementPacket)player.PacketHistory.First.Next.Value).lookrotation.eulerAngles - ((MovementPacket)player.PacketHistory.First.Value).lookrotation.eulerAngles));
                }
                else if (player.PacketHistory.Count > 0)
                {
                    Vector3 playerposition = ((MovementPacket)player.PacketHistory.First.Value).position;
                    player.Dummy.transform.position = playerposition;
                    player.Dummy.transform.rotation = ((MovementPacket)player.PacketHistory.First.Value).lookrotation;
                }
            }
        }
    }
   


}