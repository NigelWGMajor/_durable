use operationsLocal;
go
SELECT TOP (1000) [OperationFlowStateID] Id
      ,[UniqueKey] [Key]
      ,[OperationName] Operation
      ,[ActivityName] Activity
      ,[ActivityState] [State#]
      ,[ActivityStateName] [State]
      ,[Count] [Count]
      ,[Trace] Trace
      ,[InstanceId] Instance
      ,[SequenceNumber] [Sequence]
      ,[TimeStarted] [Started]
      ,[TimeEnded] [Ended]
	  ,case when timestarted = '0001-01-01 00:00:00'
	      then -1 
		  else datediff(millisecond, isnull(timestarted, getutcdate()), isnull(timeended, getutcdate())) 
	  end as Duration
      ,[TimeUpdated] Updated
  from [rpt].[OperationFlowStates]

select top 1000 OperationFlowStateHistoryId Id
  ,UniqueKey [Key]
  ,SequenceNumber [Sequence]
  ,[Count]
  ,[OperationName] Operation
  ,ActivityName Activity
  ,ActivityStateName [State] 
  ,case when TimeStarted = '0001-01-01 00:00:00'
	  then -1 
	  when TimeEnded = '0001-01-01 00:00:00'
	  then -1
	  else datediff(millisecond, TimeStarted, TimeEnded) 
  end as Duration
  ,substring(trace, 0, charindex('|', trace)) Latest
  ,Trace
from rpt.OperationFlowStateHistory 
order by UniqueKey, SequenceNumber 

-- THIS CAN BE USED TO PURGE FINISHED FILES FROM THE MAIN TABLE AND ALL FILES FROM THE HISTORY TABLE!
-- exec rpt.OperationFlowState_Purge; exec rpt.OperationFlowStateHistory_Purge;
-- OR TO REALLY SCRIUB THE SLATE
-- delete from rpt.OperationFlowStates; delete from rpt.OperationFlowStateHistory
