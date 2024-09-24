# Readme

Integration tests for validating the Flow Management engine.

In Docker:

- start the `sqlserver` instance

In the Template project:

- start azurite
- Debug (Attach to .Net Functions)

Then you can debug into the tests.

the tests project should no t include any actual project reference, or it will try to build the remote code, which will clash with 

## Testing evolution (for later process consideration)

Started by getting a trivial call with data working.
- Could verify that a call would start the orchestration 
- access the database (in this case with a non-matched record)
- would return a ready, mainly empty record
- We can initialize this more and write it the database for starting the first active cycle.

`At this point we will have tested passing parameters into the the Durable Start, the orchestration sequencing and the metadata Read/Write`

Next, build towards supporting all the scenarios!


