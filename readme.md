# Durable Template

This is being used to validate flow management beyond that normally used in Durable Functions.

Specifically, we want to manage some possibly issues with individual Activities automatically:
we want to recognize these states and respond appropriately:
    ActivityState|Meaning|Action
    --|--|--
    Stuck | In an unknown, unresponsive or timed-out state | dispose if possible and try anew
    Deferred| In a resource-depleted environment| wait and try again later
    Active| Busy doing the work| back off and give it time
    Completed| completed successfully| capture results and allow to continue
    Stalled| Had a retry-able problem| defer to the durable function retry policy in force
    Failed | Had a fatal problem| throw the exception into the durable function orchestrator and schedule its termination

The shell being developed here should allow us to test a simple sequence of activities in which we can emulate all of these conditions and verify that the Durable Function Orchestrator behaves appropriately in each case.

The initializer for the sequence is a generic object, which is then wrapped in a a product carrier for passing between steps.

In order for the system to know that an activity has been started, an external data store is needed. This simply saves the current activity and state for each orchestration each orchestration.

For this we need the simplest of data stores:

ActivityMetadata:
- `Key` a unique orchestration key
- `Activity` the Activity Identifier
- `ActivityState` unknown|Stuck|Active|Completed|Stalled|Failed
- `TimeStamp` the start time

These last three properties are collectively known as an `ActivityRecord`

The information is indexed using the `Key`, so can be implemented simply using a dictionary<string, ActivityRecord> at this stage.

The Product<T> which is serialized and passed from step to step needs to have
- T `Payload` 
- string `Key` the orchestration Id
- string `Activity` the current activity
- int `Iteration` a repeat counter for retries
- int `ProcessId` to verify if still alive
- List<ActivityRecord> `History` a log of all the activities associated with this orchestration.

[Also see this file](./NixNotes.md)