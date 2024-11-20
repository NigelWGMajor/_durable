-- FlowActivitySettings_Select

create or alter procedure [rpt].[FlowActivitySettings_Select] @ActivityName nvarchar(100)
as
begin
  with def as (
    select
        top (1) FlowActivitySettingsId,
        ActivityName,
        ActivityTimeout,
        LoadFactor,
        MaximumDelayCount,
        PartitionId
    from
        rpt.FlowActivitySettings
    where
        ActivityName = 'default'
  ),
  main as (
    select
        top (1) FlowActivitySettingsId,
        ActivityName,
        ActivityTimeout,
        LoadFactor,
        MaximumDelayCount,
        PartitionId
    from
        rpt.FlowActivitySettings
    where
        ActivityName = @ActivityName
  )
  select
    isnull(main.ActivityName, @ActivityName) ActivityName,
    isnull(main.ActivityTimeout, def.ActivityTimeout) ActivityTimeout,
    isnull(main.LoadFactor, def.LoadFactor) LoadFactor,
    isnull(main.MaximumDelayCount, def.MaximumDelayCount) MaximumDelayCount,
    isnull(main.PartitionId, def.PartitionId) PartitionId
  from
    def left join main on 1 = 1
    for json auto,   
      without_array_wrapper;
end
go