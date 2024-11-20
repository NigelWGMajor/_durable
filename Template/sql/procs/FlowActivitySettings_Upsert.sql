-- FlowActivitySettings_Upsert

create or alter procedure [rpt].[FlowActivitySettings_Upsert] @json nvarchar(max) as 
begin
  set nocount on;
  merge rpt.FlowActivitySettings as target 
  using (
    select
        json_value(@json, '$.ActivityName') as ActivityName,
        json_value(@json, '$.ActivityTimeout') as ActivityTimeout,
        json_value(@json, '$.LoadFactor') as LoadFactor,
        json_value(@json, '$.MaximumDelayCount') as MaximumDelayCount,
        json_value(@json, '$.PartitionId') as PartitionId
    ) as source on (target.ActivityName = source.ActivityName)
    when matched then
      update
      set
        ActivityName = source.ActivityName,
        ActivityTimeout = source.ActivityTimeout,
        LoadFactor = source.LoadFactor,
        MaximumDelayCount = source.MaximumDelayCount,
        PartitionId = source.PartitionId
    when not matched then
      insert
      (
        ActivityName,
        ActivityTimeout,
        LoadFactor,
        MaximumDelayCount,
        PartitionId
      )
      values
      (
        source.ActivityName,
        source.ActivityTimeout,
        source.LoadFactor,
        source.MaximumDelayCount,
        source.PartitionId
      );
end
go
