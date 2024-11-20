-- FlowActivityCanRun_Select

create or alter procedure [rpt].[FlowActivityCanRun_Select]
  @UniqueKey nvarchar(100)
as
begin
create table #Data
( 
  LoadFactor float, 
  MaximumDelayCount	int,
  RetryCount int,
  CurrentLoad float
);
insert #Data
exec rpt.FlowState_Load_Select @UniqueKey;

select case
  when d.RetryCount >= d.MaximumDelayCount then 0
  when d.LoadFactor + d.CurrentLoad > 1.0 then 0
  else 1 end  MayRun
  from #data d;

drop table #data;
end
go
