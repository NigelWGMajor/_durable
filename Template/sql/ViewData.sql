use operationsLocal;
go
SELECT TOP (1000) [OperationFlowStateID]
      ,[UniqueKey]
      ,[ActivityName]
      ,[ActivityState]
      ,[ActivityStateName]
      ,[Count]
      ,[Notes]
      ,[ProcessId]
      ,[SequenceNumber]
      ,[TimeStarted]
      ,[TimeEnded]
	  ,case when timestarted = '0001-01-01 00:00:00'
	      then -1 
		  else datediff(millisecond, isnull(timestarted, getutcdate()), isnull(timeended, getutcdate())) 
	  end as Duration
      ,[TimeUpdated]
  from [rpt].[OperationFlowStates]

select 
  UniqueKey 
  ,SequenceNumber 
  ,[count]
  ,ActivityName Activity
  ,ActivityStateName [State] 
  ,case when TimeStarted = '0001-01-01 00:00:00'
	  then -1 
	  when TimeEnded = '0001-01-01 00:00:00'
	  then -1
	  else datediff(millisecond, timestarted, timeended) 
  end as Duration
from rpt.OperationFlowStateHistory 
order by UniqueKey, SequenceNumber 

-- THESE CAN BE USED TO PURGE FINISHED FILES FROM THE MAIN TABLE AND ALL FILES FROM THE HISTORY TABLE!
-- exec rpt.OperationFlowState_Purge;
-- exec rpt.OperationFlowStateHistory_Purge;
