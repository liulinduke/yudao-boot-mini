package cn.iocoder.yudao.module.facebook.service.account;

import cn.hutool.core.collection.CollUtil;
import cn.hutool.core.util.StrUtil;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.springframework.stereotype.Service;
import jakarta.annotation.Resource;
import org.springframework.validation.annotation.Validated;
import org.springframework.transaction.annotation.Transactional;

import java.time.LocalDateTime;
import java.util.*;
import java.util.stream.Collectors;
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
    public void updateFbAccountStatus(List<Long> ids, Boolean status) {
        if (CollUtil.isEmpty(ids)) {
            return;
        }
        for (Long id : ids) {
            if (id == null) {
                continue;
            }
            FbAccountDO updateObj = new FbAccountDO();
            updateObj.setId(id);
            updateObj.setStatus(status);
            fbAccountMapper.updateById(updateObj);
        }
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
    public void updateFbAccountLanguage(Long id, String languageCode) {
        validateFbAccountExists(id);
        if (StrUtil.isBlank(languageCode)) {
            throw new IllegalArgumentException("语言代码不能为空");
        }
        FbAccountDO updateObj = new FbAccountDO();
        updateObj.setId(id);
        updateObj.setLanguageCode(languageCode);
        if ("en_US".equalsIgnoreCase(languageCode)) {
            updateObj.setLanguage(1);
        } else if (languageCode.toLowerCase(Locale.ROOT).startsWith("zh_")) {
            updateObj.setLanguage(2);
        }
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
    public void saveProfileUpload(FbAccountProfileUploadReqVO reqVO) {
        for (FbAccountProfileUploadReqVO.Item item : reqVO.getItems()) {
            FbAccountDO updateObj = new FbAccountDO();
            updateObj.setId(item.getAccountId());
            updateObj.setAvatarUrl(item.getAvatarUrl());
            updateObj.setCoverUrl(item.getCoverUrl());
            updateObj.setProfileNickname(item.getNickname());
            updateObj.setProfileSignature(item.getSignature());
            updateObj.setProfileUpdateStatus("PENDING");
            updateObj.setProfileUpdateError(null);
            fbAccountMapper.updateById(updateObj);
        }
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void reportProfileUpload(FbAccountProfileReportReqVO reqVO) {
        FbAccountDO updateObj = new FbAccountDO();
        updateObj.setId(reqVO.getAccountId());
        updateObj.setProfileUpdateStatus(reqVO.getStatus());
        updateObj.setProfileUpdateTime(java.time.LocalDateTime.now());
        updateObj.setProfileUpdateError(reqVO.getErrorMessage());
        if (reqVO.getAvatarUrl() != null) updateObj.setAvatarUrl(reqVO.getAvatarUrl());
        if (reqVO.getCoverUrl() != null) updateObj.setCoverUrl(reqVO.getCoverUrl());
        if (reqVO.getNickname() != null) updateObj.setProfileNickname(reqVO.getNickname());
        if (reqVO.getSignature() != null) updateObj.setProfileSignature(reqVO.getSignature());
        fbAccountMapper.updateById(updateObj);
    }

    @Override
    @Transactional(rollbackFor = Exception.class)
    public void updateFbAccountGroup(List<Long> ids, Long groupId) {
        for (Long id : ids) {
            FbAccountDO updateObj = new FbAccountDO();
            updateObj.setId(id);
            updateObj.setGroupId(groupId);
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
        List<String> cookieEntries = splitCookieEntries(data);
        LocalDateTime now = LocalDateTime.now();

        for (String line : cookieEntries) {
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
        // 网络异常只代表本次检测失败，不能覆盖账号原有登录状态。
        if ("NETWORK_ERROR".equalsIgnoreCase(reqVO.getLoginStatus())) {
            return;
        }
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

            JsonNode json = new ObjectMapper().readTree(cookie);
            if (json.isArray()) {
                for (JsonNode item : json) {
                    if ("c_user".equals(item.path("name").asText())) {
                        String value = item.path("value").asText();
                        if (StrUtil.isNotBlank(value)) return value;
                    }
                }
            } else if (json.isObject() && json.has("c_user")) {
                String value = json.path("c_user").asText();
                if (StrUtil.isNotBlank(value)) return value;
            }
        } catch (Exception e) {
            // ignore
        }

        return null;
    }

    private List<String> splitCookieEntries(String data) {
        String trimmedData = StrUtil.trim(data);
        if (StrUtil.isEmpty(trimmedData)) {
            return Collections.emptyList();
        }

        try {
            JsonNode json = new ObjectMapper().readTree(trimmedData);
            if (json != null && (json.isArray() || json.isObject())) {
                return Collections.singletonList(trimmedData);
            }
        } catch (Exception ignored) {
            // 不是完整 JSON 时，继续按兼容的每行 Cookie 字符串处理。
        }

        return Arrays.stream(data.split("\\r?\\n"))
                .map(String::trim)
                .filter(StrUtil::isNotEmpty)
                .collect(Collectors.toList());
    }

    private void handleEmptyCookie(FbAccountDO account) {
        if (StrUtil.isEmpty(account.getCookie())) {
            account.setCookie("[]");
        }
    }

}
