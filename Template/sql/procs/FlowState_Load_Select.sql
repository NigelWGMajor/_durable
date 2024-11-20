-- FlowState_Load_Select

create or alter procedure [rpt].[FlowState_Load_Select]
   @UniqueKey nvarchar(100)
as begin
declare @HostServer nvarchar(100);
select @HostServer = HostServer from rpt.FlowStatesInFlight where UniqueKey = @UniqueKey;

with aa as (
select
  isnull(LoadFactor, DefaultLoadFactor) LoadFactor, 
  isNull(MaximumDelayCount, DefaultMaximumDelayCount) MaximumDelayCount,
  RetryCount
  from rpt.FlowActivitySettings s join rpt.FlowStatesInFlight f
  on s.ActivityName = f.ActivityName
  left join 
(
  select 
  LoadFactor DefaultLoadfactor, 
  MaximumDelayCount DefaultMaximumDelayCount 
  from rpt.FlowActivitySettings d 
  where d.ActivityName = 'Default'
) as xxx on 1=1
where f.UniqueKey = @UniqueKey
) 
, bb as
(
select 
  sum(loadfactor) CurrentLoad
  from rpt.FlowActivitySettings s left join rpt.FlowStatesInFlight f
  on s.ActivityName = f.ActivityName
  where ActivityStateName in ('Active', 'Ready')
  and HostServer = @HostServer
) 
select * from aa join bb on 1=1;
end
go
