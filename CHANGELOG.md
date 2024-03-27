## Implemented functionality
This project is a client application, which is able to communicate with a remote server using the IPK24-CHAT protocol. The protocol has got two variants - each built on top of a different transport protocol. The implementation covers both variants. Implementation includes such libs: System.Net.Sockets System.Text.RegularExpressions System.Text

## Limitation
The project has a limitiation in speed, the speed of this project was reduced using Thread.Sleep so that there wouln't be any unexpected behaviors in case when loads of text woulb sent from server and from client at the same time. However, this limitation could be barely recognised when user doesn't know about it

## Commit history 
- Changed final systemto linux
- Refactored
- Create LICENSE
- Delete ChatClientSide directory
- Added readme
- Fixed problem with 2 times auth
- final fix
- minor fix
- Refactored the whole repo
- making makefile
- fixed minor bug
- Readjusted due to server specifications
- Refactored + fixed small bugs
- first commit
