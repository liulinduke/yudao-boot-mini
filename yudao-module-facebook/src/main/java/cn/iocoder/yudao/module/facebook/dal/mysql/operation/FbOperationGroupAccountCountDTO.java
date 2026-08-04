package cn.iocoder.yudao.module.facebook.dal.mysql.operation;

import lombok.Data;

/** 群组选择器按账号聚合后的群组数量。 */
@Data
public class FbOperationGroupAccountCountDTO {
    private String accountId;
    private Long groupCount;
}
