package cn.iocoder.yudao.module.facebook.service.agent;

import lombok.AllArgsConstructor;
import lombok.Data;
import lombok.NoArgsConstructor;

/**
 * Facebook 账号维度串行任务队列项。
 */
@Data
@NoArgsConstructor
@AllArgsConstructor
public class FbAccountTaskQueueItem {

    private String sourceType;

    private Long detailId;

    private String fbAccount;

}
