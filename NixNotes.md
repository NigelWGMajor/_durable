# Durable functions

Created a small Durable Functions test bed using [this](https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-isolated-create-first-csharp?pivots=code-editor-vscode)

Use `Azurite Start` on the command line to get azurite started.

F5 will run in the debugger: use Postman to POST to
`http://localhost:7071/api/SafeOrchestration_HttpStart`

The Output in VSCode contains azurite entries.


## Strategy

[refer to](./readme.md)
 
## manual run

F1 => Azurite:start
F5 to debug.

🔥If unable to run- with 404s, try 
- F1 => Azurite: Close
- delete __azurite* files
- F1 => Azurite: Start

<!-- Delete the obj and bin folders.

From the terminal
`cd Template`
`func start`

F5

(Ignore the popup)

Close the terminal? Then it should run the Executing task: func host start 

I am getting `Port 7071 is unavailable. Close the process using that port, or specify another port using --port [-p].`

Will try restarting everything! -->