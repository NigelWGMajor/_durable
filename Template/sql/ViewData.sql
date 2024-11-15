use operationsLocal;
go
SELECT TOP (1000) --[OperationFlowStateID] Id
      [UniqueKey] [Key]
      ,[OperationName] Operation
      ,[ActivityName] LastActivity
      --,[ActivityState] [State#]
      ,[ActivityStateName] [State]
      ,[RetryCount] [RetryCount]
	  ,[Reason] Reason
      ,[SequenceNumber] [MaxSequence]
      --,[TimeStarted] [Started]
      --,[TimeEnded] [Ended]
      --,[TimeUpdated] Updated
	  ,[HostServer]
	  ,case when isnull(timestarted,'0001-01-01 00:00:00')  = '0001-01-01 00:00:00'
	      then -1 
		  when isnull(TimeEnded, '0001-01-01 00:00:00') = '0001-01-01 00:00:00'
	  then '-'
		  else datediff(millisecond, isnull(timestarted, getutcdate()), isnull(timeended, getutcdate())) 
	  end as Duration
	  ,[Reason] FinalReason
      ,[Trace] Trace
      ,[InstanceId] Instance
  from [rpt].[OperationFlowStates]
  order by UniqueKey

select top 1000 -- OperationFlowStateHistoryId Id
  UniqueKey [Key]
  ,SequenceNumber [Sequence]
  ,[RetryCount]
  ,[OperationName] Operation
  ,ActivityName Activity
  ,ActivityStateName [State] 
  ,TimeStarted Started
  ,case when isnull(TimeStarted, '0001-01-01 00:00:00') = '0001-01-01 00:00:00'
	  then '-' 
	  when isnull(TimeEnded, '0001-01-01 00:00:00') = '0001-01-01 00:00:00'
	  then '-'
	  else 
	      format(cast(datediff(millisecond, TimeStarted, TimeEnded) as float) / 1000, '0.000')
  end as Duration
  ,Reason
  ,Trace
  ,InstanceId
from rpt.OperationFlowStateHistory 
--where UniqueKey in ('2013', '2015', '2016')
order by UniqueKey, SequenceNumber 

-- THIS CAN BE USED TO PURGE FINISHED FILES FROM THE MAIN TABLE AND ALL FILES FROM THE HISTORY TABLE!
-- exec rpt.OperationFlowState_Purge; exec rpt.OperationFlowStateHistory_Purge;
-- OR TO REALLY SCRUB THE SLATE
-- delete from rpt.OperationFlowStates; delete from rpt.OperationFlowStateHistory
