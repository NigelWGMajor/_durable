# Readme

Integration tests for validating the Flow Management engine.

In Docker:

- start the `sqlserver` instance

In the Template project:

- start azurite
- Debug (Attach to .Net Functions)

Then you can debug into the tests.


## Testing evolution (for later process consideration)

Started by getting a trivial call with data working.
- Could verify that a call would start the orchestration 
- access the database (in this case with a non-matched record)
- would return a ready, mainly empty record


