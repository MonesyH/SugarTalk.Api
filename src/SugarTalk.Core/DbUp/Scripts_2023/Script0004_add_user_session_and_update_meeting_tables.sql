create table if not exists user_session
(
    id int auto_increment
    primary key,
    `created_date` datetime(3) not null,
    `meeting_id` varchar(36) not null,
    `room_stream_id` varchar(128) null,
    `user_id` int not null,
    `is_muted` tinyint(1) default 0 null
    )
    charset=utf8mb4;

alter table `meeting` add `start_date` int not null;
alter table `meeting` add `end_date` int not null;