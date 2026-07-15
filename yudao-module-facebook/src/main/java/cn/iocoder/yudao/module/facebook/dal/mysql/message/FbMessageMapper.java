package cn.iocoder.yudao.module.facebook.dal.mysql.message;

import cn.iocoder.yudao.framework.mybatis.core.mapper.BaseMapperX;
import cn.iocoder.yudao.module.facebook.dal.dataobject.message.FbMessageDO;
import com.baomidou.mybatisplus.core.conditions.query.LambdaQueryWrapper;
import org.apache.ibatis.annotations.Mapper;

import java.util.List;

@Mapper
public interface FbMessageMapper extends BaseMapperX<FbMessageDO> {
    default FbMessageDO selectByExternalKey(Long accountId, String sourceType, String externalMessageId) {
        return selectOne(new LambdaQueryWrapper<FbMessageDO>()
                .eq(FbMessageDO::getAccountId, accountId)
                .eq(FbMessageDO::getSourceType, sourceType)
                .eq(FbMessageDO::getExternalMessageId, externalMessageId));
    }

    default List<FbMessageDO> selectByConversationId(Long conversationId) {
        return selectList(new LambdaQueryWrapper<FbMessageDO>()
                .eq(FbMessageDO::getConversationId, conversationId)
                .orderByAsc(FbMessageDO::getMessageTime)
                .orderByAsc(FbMessageDO::getId));
    }

    default FbMessageDO selectBySendDetailId(Long detailId) {
        return selectOne(new LambdaQueryWrapper<FbMessageDO>().eq(FbMessageDO::getSendDetailId, detailId));
    }
}
