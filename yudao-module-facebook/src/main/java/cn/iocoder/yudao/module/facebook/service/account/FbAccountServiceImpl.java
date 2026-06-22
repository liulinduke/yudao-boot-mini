package cn.iocoder.yudao.module.facebook.service.account;

import cn.hutool.core.collection.CollUtil;
import cn.hutool.core.util.StrUtil;
import org.springframework.stereotype.Service;
import jakarta.annotation.Resource;
import org.springframework.validation.annotation.Validated;
import org.springframework.transaction.annotation.Transactional;

import java.time.LocalDateTime;
import java.util.*;
import cn.iocoder.yudao.module.facebook.controller.admin.account.vo.*;
import cn.iocoder.yudao.module.facebook.dal.dataobject.account.FbAccountDO;
import cn.iocoder.yudao.framework.common.pojo.PageResult;
import cn.iocoder.yudao.framework.common.pojo.PageParam;
import cn.iocoder.yudao.framework.common.util.object.BeanUtils;

import cn.iocoder.yudao.module.facebook.dal.mysql.account.FbAccountMapper;

import static cn.iocoder.yudao.framework.common.exception.util.ServiceExceptionUtil.exception;
import static cn.iocoder.yudao.framework.common.util.collection.CollectionUtils.convertList;
import static cn.iocoder.yudao.framework.common.util.collection.CollectionUtils.diffList;
import static cn.iocoder.yudao.module.facebook.enums.ErrorCodeConstants.*;

/**
 * FB账号 Service 实现类
 *
 * @author 芋道源码
 */
@Service
@Validated
public class FbAccountServiceImpl implements FbAccountService {

    @Resource
    private FbAccountMapper fbAccountMapper;

    @Override
    public Long createFbAccount(FbAccountSaveReqVO createReqVO) {
        FbAccountDO fbAccount = BeanUtils.toBean(createReqVO, FbAccountDO.class);
        handleEmptyCookie(fbAccount);
        fbAccountMapper.insert(fbAccount);
        return fbAccount.getId();
    }

    @Override
    public void updateFbAccount(FbAccountSaveReqVO updateReqVO) {
        validateFbAccountExists(updateReqVO.getId());
        FbAccountDO updateObj = BeanUtils.toBean(updateReqVO, FbAccountDO.class);
        handleEmptyCookie(updateObj);
        fbAccountMapper.updateById(updateObj);
    }

    @Override
    public void deleteFbAccount(Long id) {
        validateFbAccountExists(id);
        fbAccountMapper.deleteById(id);
    }

    @Override
    public void deleteFbAccountListByIds(List<Long> ids) {
        fbAccountMapper.deleteByIds(ids);
    }

    private void validateFbAccountExists(Long id) {
        if (fbAccountMapper.selectById(id) == null) {
            throw exception(FB_ACCOUNT_NOT_EXISTS);
        }
    }

    @Override
    public FbAccountDO getFbAccount(Long id) {
        return fbAccountMapper.selectById(id);
    }

    @Override
    public PageResult<FbAccountDO> getFbAccountPage(FbAccountPageReqVO pageReqVO) {
        return fbAccountMapper.selectPage(pageReqVO);
    }

    @Override
    public void updateFbAccountLanguage(Long id, Integer language) {
        validateFbAccountExists(id);
        if (language != 1 && language != 2) {
            throw new IllegalArgumentException("语言设置只能是1(英文)或2(中文)");
        }
        FbAccountDO updateObj = new FbAccountDO();
        updateObj.setId(id);
        updateObj.setLanguage(language);
        fbAccountMapper.updateById(updateObj);
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void updateFbAccountProxy(List<Long> ids, Long proxyId) {
        for (Long id : ids) {
            FbAccountDO updateObj = new FbAccountDO();
            updateObj.setId(id);
            updateObj.setProxyId(proxyId);
            fbAccountMapper.updateById(updateObj);
        }
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void importFbAccount(FbAccountImportReqVO importReqVO) {
        String data = importReqVO.getData();
        String[] lines = data.split("\n");
        LocalDateTime now = LocalDateTime.now();

        for (String line : lines) {
            line = line.trim();
            if (StrUtil.isEmpty(line)) {
                continue;
            }

            String[] parts = line.split("----");
            if (parts.length < 2) {
                continue;
            }

            String userName = parts[0].trim();
            String password = parts[1].trim();
            String securityKey = parts.length > 2 ? parts[2].trim() : null;

            if (StrUtil.isEmpty(userName) || StrUtil.isEmpty(password)) {
                continue;
            }

            FbAccountDO account = new FbAccountDO();
            account.setFbAccount(userName);
            account.setPassword(password);
            account.setTfa(securityKey);
            account.setGroupId(importReqVO.getGroupId());
            account.setProxyId(importReqVO.getProxyId());
            account.setStatus(true);
            account.setCreateTime(now);
            account.setUpdateTime(now);
            handleEmptyCookie(account);

            fbAccountMapper.insert(account);
        }
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void importFbAccountCookie(FbAccountCookieImportReqVO importReqVO) {
        String data = importReqVO.getData();
        String[] lines = data.split("\n");
        LocalDateTime now = LocalDateTime.now();

        for (String line : lines) {
            line = line.trim();
            if (StrUtil.isEmpty(line)) {
                continue;
            }

            String userId = extractUserIdFromCookie(line);
            if (StrUtil.isEmpty(userId)) {
                continue;
            }

            FbAccountDO account = new FbAccountDO();
            account.setFbAccount(userId);
            account.setCookie(line);
            account.setGroupId(importReqVO.getGroupId());
            account.setProxyId(importReqVO.getProxyId());
            account.setStatus(true);
            account.setCreateTime(now);
            account.setUpdateTime(now);

            fbAccountMapper.insert(account);
        }
    }

    @Override
    public void updateFbAccountLoginResult(FbAccountLoginResultUpdateReqVO reqVO) {
        validateFbAccountExists(reqVO.getId());

        FbAccountDO updateObj = new FbAccountDO();
        updateObj.setId(reqVO.getId());
        updateObj.setLoginStatus(reqVO.getLoginStatus());
        updateObj.setLoginErrorReason(reqVO.getLoginErrorReason());
        if (StrUtil.isNotBlank(reqVO.getCookie())) {
            updateObj.setCookie(reqVO.getCookie());
        }
        if ("SUCCESS".equalsIgnoreCase(reqVO.getLoginStatus())) {
            updateObj.setLoginErrorReason(null);
            updateObj.setLastLoginTime(LocalDateTime.now());
        }
        fbAccountMapper.updateById(updateObj);
    }

    private String extractUserIdFromCookie(String cookie) {
        if (StrUtil.isEmpty(cookie)) {
            return null;
        }

        try {
            int start = cookie.indexOf("c_user=");
            if (start != -1) {
                int end = cookie.indexOf(";", start);
                if (end == -1) {
                    end = cookie.length();
                }
                return cookie.substring(start + 7, end);
            }
        } catch (Exception e) {
            // ignore
        }

        return null;
    }

    private void handleEmptyCookie(FbAccountDO account) {
        if (StrUtil.isEmpty(account.getCookie())) {
            account.setCookie("[]");
        }
    }

}
