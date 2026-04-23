package cn.iocoder.yudao.module.facebook.enums;

import cn.iocoder.yudao.framework.common.exception.ErrorCode;

// TODO 待办：请将下面的错误码复制到 yudao-module-facebook 模块的 ErrorCodeConstants 类中。注意，请给“TODO 补充编号”设置一个错误码编号！！！
/**
 * System 错误码枚举类
 *
 * system 系统，使用 2-002-000-000 段
 */
public interface ErrorCodeConstants {
    ErrorCode FB_ACCOUNT_NOT_EXISTS = new ErrorCode(2_001_000_000, "FB账号不存在");
    ErrorCode FB_COLLECT_NOT_EXISTS = new ErrorCode(2_002_000_000, "FB采集任务不存在");
    ErrorCode FB_COLLECT_USER_NOT_EXISTS = new ErrorCode(2_003_000_000, "FB采集任务用户不存在");
    ErrorCode FB_COLLECT_POST_NOT_EXISTS = new ErrorCode(2_004_000_000, "FB采集任务帖子不存在");
    ErrorCode FB_COLLECT_GROUP_NOT_EXISTS = new ErrorCode(2_005_000_000, "FB采集任务群组不存在");

    // 运营任务错误码
    ErrorCode OPERATION_TASK_NOT_EXISTS = new ErrorCode(2_006_000_000, "运营任务不存在");
    ErrorCode OPERATION_TASK_DETAIL_NOT_EXISTS = new ErrorCode(2_006_001_000, "运营任务明细不存在");




}