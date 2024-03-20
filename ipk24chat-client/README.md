## IPK Client for a chat server using IPK24-CHAT protocol
Author: Nikita Koliada
This project's task is to design and implement a client application, which is able to communicate with a remote server using the IPK24-CHAT protocol. The protocol has got two variants - each built on top of a different transport protocol.The implementation covers both variants.

### Installation
> **Note!**
The program is made for UNIX-based systems only. In other cases the client side chat won't work.

To install the client side chat, follow these steps:
1. Download the source code from the repository/archive.
2. Compile the source code using Makefile:
```bash
make
```
### Usage

```bash
./ipk24chat-client -t [tcp/udp] -s [server] -p [port] -d [udpConfirmationTimeout] -r [maxRetransmissions]
```
 - -t: Transport protocol used for connection
 - -s: Server IP or hostname
 - -p: Server port
 - -d: UDP confirmation timeout
 - -r: Maximum number of UDP retransmissions
 - -h: Prints program help output and exits

### Input

- `/auth {Username} {Secret} {DisplayName}` - Authenticate with the server.
- `/join {ChannelID}` - Join a chat channel.
- `/rename {DisplayName}` - Change your display name.
- `/help` - Show this help message.
- Any other appropriate text will be sent as messages

### An example of output and input 
 
```
/auth xkolia00 abc123 nikita
Success: Authentication successful.
Server: nikita joined general.
Hey
/rename nk
/join room
Server: nk joined room
Success: Channel room successfully joined.
Server: another_user joined room
user: sup
/join room2
Server: nl joined room2
Success: Channel room2 successfully joined.
/bye
```

### Types of messages 

| Type name | Description |
| --------- | ----------- |
| `ERR`     | Indicates that an error has occurred while processing the other party's last message, this eventually results in the termination of the communication. |
| `CONFIRM` | Only leveraged in certain protocol variants (UDP) to explicitly confirm the successful delivery of the message to the other party on the application level. |
| `REPLY`   | Some messages (requests) require a positive/negative confirmation from the other side, this message contains such data. |
| `AUTH`    | Used for client authentication (signing in) using user-provided username, display name, and a password. |
| `JOIN`    | Represents client's request to join a chat channel by its identifier. |
| `MSG`     | Contains user display name and a message designated for the channel they've joined in. |
| `BYE`     | Either party can send this message to indicate that the conversation/connection is to be terminated. This is the final message sent in a conversation (except its potential confirmations in UDP). |

### Protocols
The program works with limitted amount of protocols. Such as:

##### IPv4 TCP
TCP packets are used for reliable, ordered, and error-checked
delivery of data between applications over an IP network. TCP packets operate
at the Transport layer (Layer 4) of the OSI model.

##### IPv4 UDP
UDP is a transport protocol used for sending data over IP networks. It is a connectionless protocol that does not guarantee reliable delivery of data or error checking. Mostly it is used in cases when amount of data is more required than its quality (such as streaming)
In this protocol in order to detect a packet loss will be using mandatory message confirmation with timeouts. Once a message is sent it is required to confirm its successful delivery by the other party. The confirmation should be sent immediately after receiving the message, regardless of any potential higher-level processing issues - unless the connection has already been successfully terminated, in such a case it is valid not to respond to the message at all. When the confirmation is not received by the original sender within a given timespan, the message is considered lost in transit. Messages lost in transit are re-transmitted until the confirmation is successfully received or an internal re-try condition is triggered. Only the original message sender performs message retransmit, not receiver (confirmation messages are never re-transmitted or explicitly confirmed).

## Testing
During the testing of my app I used several tools to check its functionality - 3 main tools were nc in the terminal, which was used mainly for checking the right format of input - output for tcp, wireshark which I used to track udp packets send and received by my app and the discord server which I could test as the main source of correct connections and transmissions. I also build a ipk24chat-server program which is the 2 project, so I could test multiple connections at a time.

#### Examples
This particulat test case was testing the correct format of sent and received data on a client side ( as well as on server side). The testing was conducted using two separate terminals(one for client, other for server). The testing environment was linux based os.

###### Input
```
/auth xkolia00 x nk
/join room1
Hey
/rename nik
Hey
/join room2
test in second chat 
/rename niki
test using niki 
/join room3
test in third chat
/bye
```
###### Output

```
Success: Auth success
Server: nk has joined default
Server: nk has joined room1
Server: nik has joined room2
Server: niki has joined room3
```
###### Output on my server


```
RECV 127.0.0.1:64147 | AUTH
SENT 127.0.0.1:64147 | REPLY
RECV 127.0.0.1:64147 | JOIN
SENT 127.0.0.1:64147 | REPLY
RECV 127.0.0.1:64147 | MSG
RECV 127.0.0.1:64147 | MSG
RECV 127.0.0.1:64147 | JOIN
SENT 127.0.0.1:64147 | REPLY
RECV 127.0.0.1:64147 | MSG
RECV 127.0.0.1:64147 | MSG
RECV 127.0.0.1:64147 | JOIN
SENT 127.0.0.1:64147 | REPLY
RECV 127.0.0.1:64147 | MSG
RECV 127.0.0.1:64147 | BYE
```

### Bibliography
[TCP/IP layers, IPv4 protocols](https://book.huihoo.com/iptables-tutorial/c171.htm)
https://datatracker.ietf.org/doc/html/rfc2119
https://datatracker.ietf.org/doc/html/rfc5234
As well as other youtube/stackoverflow resources