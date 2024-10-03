## Updated workflow

Azurite: 
- Set the flag to use in-memory processing, which will stop all Azurite processes when Azurite is stopped.

- Sql Server: use a 🐳 docker image.
  - in Sql/docker folder, `docker-compose.yml` and `Dockerfile.sql`

In the docker folder, 

- To create a new empty Sql Database:
  `docker buildx build -t sql-hybrid -f Dockerfile.sql --no-cache`
- To restart an existing docker image:
  `docker-compose up d`
  on windows:
  `"C:\Program Files (x86)\Microsoft SQL Server Management Studio 18\Common7\IDE\Ssms.exe" -nosplash`
- Logging in from SSMS:
  `Server type:    Database Engine`
  `Server Name:    127.0.0.1:1433`
  `Authentication: SQL Server Authentication`
  `Login:          sa`
  `Password:       Dock2Gently`
                  `[x] Remember password`
- Logging in from code (Microsoft Sql Client):
  `"Server=127.0.0.1,1433;Database=OperationsLocal;User Id=sa;Password=Dock2Gently;MultipleActiveResultSets=True;TrustServerCertificate=true;"`

- After building an empty Sql server
  - Log in using SSMS
  - Copy the content of init.sql to a new query window
  - Run the query
  Your database can be started and stopped in Docker Desktop as needed: it will save the data.
  The database you will use is `OperationsLocal`

The durable functions run in Azurite.
To save some trouble deleting azurite files, you might want to check the setting 
  `Azurite: In Memory Persistence`.

F1 ~> Azurite:Start 

The try Debug page ~> Attach to .NET Functions (F5)

The terminal will show a bunch of build activity, and eventually should a line 
` *  Executing task: func host start`

Which indicates that the durable functions are starting up. 
Be patient, as long as you are not seeing red messages.
... when a blue line like 
`Host lock lease acquired by instance ID '00000000000000000000000071EE63C5'.` shows, you are ready to test.

## SSMS

Copy the ViewData.sql script to a query window in SSMS.
This can be used to examine the data quickly. 
If you need them, there are a couple of lines at the bottom to purge the database.

## Testing or debugging

With the Durable functions running in Azurite and SSMS verifying the database content, the system can be tested.

The simplest way to do this is to use the emulation-test-scenarios.http file, running with the 
[httpyac VSCode extension](https://httpyac.github.io) and httpnotebook.

The scenarios are set up to inject various disruptions, which can be verified through their consequences and/or by setting breakpoints in the code. 

Beware of the fact that breakpoints in orchestrations can be misleading:

- every time `await context.CallSubOrchestratorAsync<Product>(...` is executed, as happens in the main orchestration
- and every time `await context.CallActivityAsync<Product>( ...` is executed, as happens three times in each sub-orchestrator,
the product returned from the first call (when not replaying) is stored by the durable framework and the thread is abandoned: on the next pass, the durable framework replays the entire chain from the beginning, retrieving the previously stored products at each step while replaying.
As a result, the early steps are hit repeatedly but only the first hit actually calls the sub-orchestration or activity.

You can set breakpoints in Activities, as they are not replayed in the same way as the orchestrations are. 

The sub-orchestrations in this pattern are each intended to wrap a single activity in opening and closing interactions with metadata. 
Accordingly, most of their code is boilerplate, except for the name of the activity, and the flags passed to determine the retry policy to be applied.
The main orchestration's responsibility is to determine the correct sequence, and define the flags for the overall retries.

Accordingly, each sub-orchestration is only run once (per whole operation), the rest of the time it is replaying, unless the built-in retry mechanism has kicked in. 

The main orchestration calls three Sub-orchestrations (Alpha, Bravo and Charlie) in turn, followed by an activity to `Finish` the operation.
Each sub-orchestration calls activities to `PreProcess`, perform the desired `activity`, and `PostProcess`.

Logically, the flow is

main -> subA -> preA -> ActivityA -> PostA -> 
        subB -> preB -> ActivityB -> PostB -> 
        subC -> preC -> ActivityC -> PostC -> finish

The actual sequence, as would be encountered using breakpoints, is a series of Queueing, Running, Storing and Retrieving operations and Durable Executions:

start(input0) => STO:Input0 QUE:main(Input0)
XEQ:main -> RCL:Input0 -> STO:ProductA -> QUE:subA(ProductA) 
XEQ:main -> RCL:Input0 -> RCL:ProductA -> XEQ:subA(ProductA) 
XEQ:main -> RCL:ProductA -> XEQ:subA(ProductA) -> STO:ProductA0 -> QUE:subA.Pre(ProductA0)
XEQ:main -> RCL:ProductA -> XEQ:subA(ProductA) -> RCL:ProductA0 -> RUN:subA.Pre(ProductA0) -> STO:ProductA1
XEQ:main -> RCL:ProductA -> XEQ:subA(ProductA) -> RCL:ProductA0 -> RCL:ProductA1 -> QUE:SubA.Activity(ProductA1)
XEQ:main -> RCL:ProductA -> XEQ:subA(ProductA) -> RCL:ProductA0 -> RCL:ProductA1 -> RUN:SubA.Activity(ProductA1) -> STO:productA2 
XEQ:main -> RCL:ProductA -> XEQ:subA(ProductA) -> RCL:ProductA0 -> RCL:ProductA1 -> RCL:productA2 -> QUE:subA.Post(productA2)
XEQ:main -> RCL:ProductA -> XEQ:subA(ProductA) -> RCL:ProductA0 -> RCL:ProductA1 -> RCL:productA2 -> RUN:subA.Post(productA2) -> STO:ProductA3
XEQ:main -> RCL:ProductA -> XEQ:subA(ProductA) -> RCL:ProductA0 -> RCL:ProductA1 -> RCL:productA2 -> RCL:ProductA3 -> Return -> STO:ProductSubA
XEQ:main -> RCL:ProductA -> RCL:ProductSubA -> XEQ:subB(ProductSubA) ... 

Congratulations, you've completed the first activity! You can see how as the depth increases, the threads become confusing.

To simplify breakpoints, it can be better to put breakpoints on the underlying models to see when properties change.
