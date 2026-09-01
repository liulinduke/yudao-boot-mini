<template>
  <div class="ai-agent-page">
    <el-row :gutter="12">
      <el-col :span="8">
        <ContentWrap>
          <div class="panel-title">创建Agent</div>
          <div class="panel-subtitle">创建 Facebook AI 自动获客 Agent</div>
          <div class="agent-entry-list">
            <div
              v-for="item in agentEntries"
              :key="item.type"
              class="agent-entry"
              :class="{ disabled: item.disabled }"
              @click="openCreateWizard(item)"
            >
              <div class="agent-entry__header">
                <Icon :icon="item.icon" :size="18" />
                <span>{{ item.title }}</span>
              </div>
              <div class="agent-entry__desc">{{ item.description }}</div>
              <el-tag size="small" :type="item.disabled ? 'info' : 'success'">
                {{ item.disabled ? '开发中' : '可创建' }}
              </el-tag>
            </div>
          </div>
        </ContentWrap>
      </el-col>

      <el-col :span="16">
        <ContentWrap>
          <div class="list-header">
            <div>
              <div class="panel-title">Agent列表</div>
            </div>
            <el-button :loading="dispatching" type="primary"  :disabled="!selectedAgentIds.length" @click="handleDispatch">
              <Icon icon="ep:video-play" class="mr-5px" /> 立即执行
            </el-button>
          </div>

          <el-form :model="queryParams" inline class="search-form">
            <el-form-item label="名称">
              <el-input v-model="queryParams.agentName" clearable class="!w-180px" />
            </el-form-item>
            <el-form-item label="类型">
              <el-select v-model="queryParams.agentType" clearable class="!w-150px">
                <el-option label="AI群帖获客" value="group_post" />
                <el-option label="AI帖子获客" value="post_lead" />
                <el-option label="AI公共主页获客" value="page_lead" />
                <el-option label="AI群帖评论截流" value="group_comment" />
                <el-option label="AI竞品监控" value="competitor_buyer" />
              </el-select>
            </el-form-item>
            <el-form-item label="状态">
              <el-select v-model="queryParams.status" clearable class="!w-130px">
                <el-option label="草稿" :value="0" />
                <el-option label="运行中" :value="1" />
                <el-option label="暂停" :value="2" />
                <el-option label="停止" :value="3" />
              </el-select>
            </el-form-item>
            <el-form-item>
              <el-button @click="handleQuery">
                <Icon icon="ep:search" class="mr-5px" /> 搜索
              </el-button>
            </el-form-item>
          </el-form>

          <el-table
            v-loading="loading"
            :data="list"
            :show-overflow-tooltip="true"
            @selection-change="handleSelectionChange"
          >
            <el-table-column type="selection" width="45" />
            <el-table-column label="Agent名称" prop="agentName" min-width="180" />
            <el-table-column label="类型" width="130">
              <template #default="scope">{{ getAgentTypeLabel(scope.row.agentType) }}</template>
            </el-table-column>
            <el-table-column label="状态" width="110">
              <template #default="scope">
                <el-tag :type="getStatusTagType(scope.row.status)">
                  {{ getStatusLabel(scope.row.status) }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column label="线索" width="90">
              <template #default="scope">{{ scope.row.leadCount || 0 }}</template>
            </el-table-column>
            <el-table-column label="达标客户数" width="100">
              <template #default="scope">{{ scope.row.qualifiedCount || 0 }}</template>
            </el-table-column>
            <el-table-column label="已触达" width="90">
              <template #default="scope">{{ scope.row.touchedCount || 0 }}</template>
            </el-table-column>
            <el-table-column label="未触达" width="90">
              <template #default="scope">{{ scope.row.untouchedCount || 0 }}</template>
            </el-table-column>
            <el-table-column label="发现来源" min-width="180">
              <template #default="scope">
                <template v-if="isSourceUrlAgent(scope.row.agentType)">
                  {{ getMonitorUrls(scope.row).slice(0, 3).join(' / ') || '-' }}
                </template>
                <template v-else>
                  {{ parseJsonArray<string>(scope.row.keywordPool).slice(0, 4).join(' / ') || '-' }}
                </template>
              </template>
            </el-table-column>
            <el-table-column label="执行时间" width="100">
              <template #default="scope">{{ scope.row.executeTime || '09:00' }}</template>
            </el-table-column>
            <el-table-column label="上次执行" width="160">
              <template #default="scope">{{ formatDateTime(scope.row.lastExecuteTime) }}</template>
            </el-table-column>
            <el-table-column label="创建时间" width="160">
              <template #default="scope">{{ formatDateTime(scope.row.createTime) }}</template>
            </el-table-column>
            <el-table-column label="操作" width="260" fixed="right">
              <template #default="scope">
                <el-button link type="primary" @click="handleEdit(scope.row)">编辑</el-button>
                <el-button
                  v-if="scope.row.status === 1"
                  link
                  type="warning"
                  @click="handleStatus(scope.row, 2)"
                >
                  暂停
                </el-button>
                <el-button
                  v-else
                  link
                  type="success"
                  @click="handleStatus(scope.row, 1)"
                >
                  {{ scope.row.status === 0 ? '启动' : '恢复' }}
                </el-button>
                <el-button link type="primary" @click="handleView(scope.row)">详情</el-button>
                <el-button link type="danger" @click="handleDelete(scope.row)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>

          <Pagination
            :total="total"
            v-model:page="queryParams.pageNo"
            v-model:limit="queryParams.pageSize"
            @pagination="getList"
          />
        </ContentWrap>
      </el-col>
    </el-row>

    <Dialog v-model="wizardVisible" :title="wizardTitle" width="980px">
      <el-steps :active="wizardStep" finish-status="success" class="mb-20px">
        <el-step title="客户来源" />
        <el-step title="发现策略" />
        <el-step title="触达策略" />
        <el-step title="AI业务员" />
      </el-steps>

      <el-form ref="wizardFormRef" :model="wizardForm" label-width="110px" :rules="wizardRules">
        <template v-if="wizardStep === 0">
          <el-form-item label="Agent名称" prop="agentName">
            <el-input v-model="wizardForm.agentName" placeholder="例如：美国卫浴客户开发" />
          </el-form-item>
          <el-form-item v-if="!isSourceUrlAgent(wizardForm.agentType)" label="搜索方式" prop="searchMode">
            <el-radio-group v-model="wizardForm.searchMode">
              <el-radio-button label="keyword">关键词搜索</el-radio-button>
              <el-radio-button label="link">链接搜索</el-radio-button>
            </el-radio-group>
          </el-form-item>
          <el-form-item
            v-if="!isSourceUrlAgent(wizardForm.agentType) && wizardForm.searchMode === 'link'"
            label="搜索链接模板"
            prop="searchUrlTemplate"
          >
            <el-input
              v-model="wizardForm.searchUrlTemplate"
              type="textarea"
              :rows="3"
              placeholder="粘贴 Facebook 搜索条件链接，系统只替换 q 参数并保留 filters 条件"
            />
          </el-form-item>
          <el-form-item label="主营/出口产品说明" prop="exportProduct">
            <el-input
              v-model="wizardForm.exportProduct"
              type="textarea"
              :rows="4"
              :maxlength="255"
              show-word-limit
              placeholder="请描述产品类别、核心规格/材质、目标客户和优势。&#10;例如：我们生产出口中高端卫浴龙头及淋浴系统，主要材质为黄铜和不锈钢，提供 OEM/ODM，面向建材批发商、卫浴品牌商和工程项目采购商。"
            />
            <div class="form-tip">填写越详细，越有助于 AI 进行客户匹配和关键词扩展。</div>
          </el-form-item>
          <template v-if="isGroupAgent(wizardForm.agentType)">
            <el-form-item label="群组来源">
              <el-radio-group v-model="wizardState.groupPostSourceMode">
                <el-radio-button label="manual">手动输入</el-radio-button>
                <el-radio-button label="select">资源库选择</el-radio-button>
              </el-radio-group>
            </el-form-item>
            <el-form-item v-if="wizardState.groupPostSourceMode === 'manual'" label="群组链接">
              <el-input
                v-model="wizardState.manualGroupUrlsText"
                type="textarea"
                :rows="5"
                placeholder="每行一个 Facebook 群组链接，例如&#10;https://www.facebook.com/groups/xxx"
              />
            </el-form-item>
            <el-form-item v-else label="选择群组">
              <div class="w-full">
                <el-button type="primary" @click="groupSelectorVisible = true">
                  <Icon icon="ep:plus" class="mr-5px" /> 选择群组
                </el-button>
                <div v-if="wizardState.selectedGroups.length" class="mt-10px">
                  <el-tag
                    v-for="group in wizardState.selectedGroups"
                    :key="group.id"
                    closable
                    class="mr-8px mb-8px"
                    @close="removeSelectedGroup(group.id)"
                  >
                    {{ group.groupName || group.url }}
                  </el-tag>
                </div>
              </div>
            </el-form-item>
          </template>
          <template v-if="isCompetitorAgent(wizardForm.agentType)">
            <el-form-item label="竞品主页来源">
              <el-radio-group v-model="wizardState.competitorSourceMode">
                <el-radio-button label="manual">手动输入</el-radio-button>
                <el-radio-button label="select">资源库选择</el-radio-button>
              </el-radio-group>
            </el-form-item>
            <el-form-item v-if="wizardState.competitorSourceMode === 'manual'" label="Page链接">
              <el-input
                v-model="wizardState.manualCompetitorPageUrlsText"
                type="textarea"
                :rows="5"
                placeholder="每行一个 Facebook Page 主页链接，例如&#10;https://www.facebook.com/pageName"
              />
            </el-form-item>
            <el-form-item v-else label="选择主页">
              <div class="w-full">
                <el-button type="primary" @click="userSelectorVisible = true">
                  <Icon icon="ep:plus" class="mr-5px" /> 选择潜客主页
                </el-button>
                <div v-if="wizardState.selectedCompetitorPages.length" class="mt-10px">
                  <el-tag
                    v-for="user in wizardState.selectedCompetitorPages"
                    :key="user.id"
                    closable
                    class="mr-8px mb-8px"
                    @close="removeSelectedCompetitorPage(user.id)"
                  >
                    {{ user.userName || user.url }}
                  </el-tag>
                </div>
              </div>
            </el-form-item>
          </template>
          <el-form-item
            label="账号池"
            prop="accountIds"
            :rules="wizardState.accountSelectionMode === 'MANUAL' ? wizardRules.accountIds : []"
          >
            <FbAccountSelector v-model="wizardState.accountIdList" v-model:selection-mode="wizardState.accountSelectionMode" scene="agent" class="w-full" />
          </el-form-item>
        </template>

        <template v-else-if="wizardStep === 1">
          <el-form-item v-if="!isSourceUrlAgent(wizardForm.agentType)" label="目标客户数量" prop="targetCustomerCount">
            <el-input-number v-model="wizardForm.targetCustomerCount" :min="1" :max="100000" />
          </el-form-item>
          <el-form-item label="执行频率">
            <el-select v-model="wizardForm.executeFrequency" class="!w-180px">
              <el-option v-for="day in intervalDayOptions" :key="day" :label="day === 1 ? '每天' : `每隔${day}天`" :value="String(day)" />
            </el-select>
          </el-form-item>
          <el-form-item label="执行时间" prop="executeTime">
            <el-time-picker
              v-model="wizardForm.executeTime"
              format="HH:mm"
              value-format="HH:mm"
              placeholder="选择执行时间"
              class="!w-180px"
            />
          </el-form-item>
          <template v-if="isSourceUrlAgent(wizardForm.agentType)">
            <el-form-item label="采集最近">
              <div class="inline-row">
                <el-input-number
                  v-model="wizardState.recentDays"
                  :min="1"
                  :max="30"
                  @change="markRecentDaysCustomized"
                />
                <span>{{ isCompetitorAgent(wizardForm.agentType) ? '天主页帖子' : '天群帖' }}</span>
              </div>
              <div class="form-tip">建议采集天数不超过执行频率周期（{{ collectionCycleDays }}天），否则可能采集到重复内容。</div>
            </el-form-item>
          </template>

          <template v-else>
          <el-form-item v-if="isPostLeadAgent(wizardForm.agentType) && wizardForm.searchMode !== 'link'" label="帖子过滤">
            <el-checkbox v-model="wizardState.latestPosts">最新帖</el-checkbox>
          </el-form-item>
           <el-form-item label="种子关键词" prop="seedKeywords">
            <el-input
              v-model="wizardState.seedKeywordsText"
              type="textarea"
              :rows="5"
              placeholder="每行一个关键词，例如&#10;bathroom faucet&#10;bath faucet&#10;shower faucet"
             />
           </el-form-item>
           <el-form-item label="关键词生成语言">
             <el-input
               v-model="wizardState.targetLanguage"
               placeholder="例如 English、中文、Spanish"
               class="!w-180px"
               clearable
             />
           </el-form-item>
           <el-form-item label="AI扩展关键词">
            <div class="inline-row">
              <el-checkbox v-model="wizardForm.aiKeywordExpandEnabled">AI扩展关键词</el-checkbox>
              <el-input-number
                v-model="wizardForm.aiKeywordExpandCount"
                :min="1"
                :max="100"
                :disabled="!wizardForm.aiKeywordExpandEnabled"
              />
              <el-button
                :disabled="!wizardForm.aiKeywordExpandEnabled"
                :loading="generatingKeywords"
                @click="handleGenerateKeywords"
              >
                生成关键词
              </el-button>
            </div>
          </el-form-item>
          <el-form-item label="关键词池" prop="keywordPool">
            <div class="keyword-pool">
              <el-tag
                v-for="(item, index) in wizardState.keywordPoolList"
                :key="`${item}-${index}`"
                closable
                class="keyword-tag"
                @close="removeKeyword(index)"
              >
                {{ item }}
              </el-tag>
            </div>
            <div class="inline-row mt-10px">
              <el-input
                v-model="wizardState.newKeyword"
                placeholder="新增关键词"
                class="!w-260px"
                @keyup.enter="appendKeyword"
              />
              <el-button @click="appendKeyword">新增</el-button>
            </div>
          </el-form-item>
          <el-form-item label="每轮执行关键词" prop="keywordsPerRun">
            <el-input-number
              v-model="wizardForm.keywordsPerRun"
              :min="1"
              :max="Math.max(wizardState.keywordPoolList.length || 1, 1)"
            />
          </el-form-item>
          </template>
        </template>

        <template v-else-if="wizardStep === 2">
          <el-form-item v-if="wizardForm.agentType !== 'group_comment' && wizardForm.agentType !== 'competitor_buyer'" label="自动评论">
            <el-switch v-model="wizardForm.autoCommentEnabled" />
          </el-form-item>
          <el-form-item label="自动私信">
            <el-switch v-model="wizardForm.autoDmEnabled" />
          </el-form-item>
          <el-form-item label="随机间隔">
            <div class="inline-row">
              <el-input-number v-model="wizardState.replyDelayMin" :min="1" :max="1440" />
              <span>至</span>
              <el-input-number v-model="wizardState.replyDelayMax" :min="1" :max="1440" />
              <span>分钟</span>
            </div>
          </el-form-item>
          <el-form-item prop="touchScoreThreshold">
            <template #label>
              <el-tooltip :content="intentLevelTip" placement="top">
                <span class="form-label-tip">触达意向等级</span>
              </el-tooltip>
            </template>
            <el-select v-model="wizardForm.touchScoreThreshold" class="!w-220px">
              <el-option
                v-for="item in intentLevelOptions"
                :key="item.value"
                :label="item.thresholdLabel"
                :value="item.value"
              />
            </el-select>
          </el-form-item>
        </template>

        <template v-else>
          <el-form-item label="业务员人设" prop="personaType">
            <el-select v-model="wizardForm.personaType" class="!w-300px" popper-class="persona-select-dropdown">
              <el-option
                v-for="item in personaOptions"
                :key="item.value"
                :label="item.label"
                :value="item.value"
              >
                <div class="persona-option">
                  <span>{{ item.label }}</span>
                  <span>{{ item.description }}</span>
                </div>
              </el-option>
            </el-select>
            <div class="form-tip">{{ selectedPersonaOption?.description }}</div>
          </el-form-item>
        </template>
      </el-form>

      <template #footer>
        <div class="dialog-footer">
          <el-button v-if="wizardStep > 0" @click="wizardStep--">上一步</el-button>
          <el-button v-if="wizardStep < 3" type="primary" @click="nextStep">下一步</el-button>
          <el-button v-else type="primary" :loading="saving" @click="submitWizard">保存Agent</el-button>
        </div>
      </template>
    </Dialog>

    <el-drawer v-model="detailVisible" title="Agent详情" size="70%">
      <template v-if="detailAgent">
        <div class="detail-head">
          <div>
            <div class="detail-title">{{ detailAgent.agentName }}</div>
            <div class="panel-subtitle">{{ getAgentTypeLabel(detailAgent.agentType) }}</div>
          </div>
          <el-tag :type="getStatusTagType(detailAgent.status)">
            {{ getStatusLabel(detailAgent.status) }}
          </el-tag>
        </div>

        <el-tabs v-model="detailTab">
          <el-tab-pane label="客户发现" name="discovery">
            <el-table v-loading="discoveryLoading" :data="discoveryList" :show-overflow-tooltip="true">
              <el-table-column label="发现时间" width="170">
                <template #default="scope">{{ formatDateTime(scope.row.createTime || scope.row.updateTime) }}</template>
              </el-table-column>
              <el-table-column v-if="!isHideKeywordColumn(detailAgent?.agentType)" label="关键词" prop="keyword" min-width="150" />
              <el-table-column label="发现来源" width="110">
                <template #default="scope">{{ getDiscoverySourceLabel(scope.row.sourceType) }}</template>
              </el-table-column>
              <el-table-column label="发现客户数" prop="discoveredCount" width="110" />
              <el-table-column label="AI分析" prop="aiAnalyzeCount" width="90" />
              <el-table-column label="达标客户数" prop="highIntentCount" width="110" />
              <el-table-column label="未达阈值" prop="filteredCount" width="100" />
              <el-table-column label="已触达" prop="touchedCount" width="90" />
            </el-table>
            <Pagination
              :total="discoveryTotal"
              v-model:page="discoveryPage.pageNo"
              v-model:limit="discoveryPage.pageSize"
              @pagination="loadDiscoveryPage"
            />
          </el-tab-pane>

          <el-tab-pane label="线索列表" name="leads">
            <el-table v-loading="leadLoading" :data="leadList" :show-overflow-tooltip="true" @sort-change="handleLeadSortChange">
              <el-table-column v-if="!isPostLeadOrGroupPostAgent(detailAgent?.agentType) && !isCommentLeadAgent(detailAgent?.agentType)" label="线索名称" min-width="160">
                <template #default="scope">{{ scope.row.userName || scope.row.postUser || '-' }}</template>
              </el-table-column>
              <el-table-column v-if="isCommentLeadAgent(detailAgent?.agentType)" label="评论用户" min-width="160">
                <template #default="scope">{{ scope.row.userName || scope.row.commentUser || '-' }}</template>
              </el-table-column>
              <el-table-column v-if="isCommentLeadAgent(detailAgent?.agentType)" label="评论者主页" min-width="220">
                <template #default="scope">
                  <el-link v-if="scope.row.url" :href="scope.row.url" target="_blank" type="primary">
                    {{ scope.row.url }}
                  </el-link>
                  <span v-else>-</span>
                </template>
              </el-table-column>
              <el-table-column label="采集账号" prop="fbAccount" width="140" />
              <el-table-column label="客户类型" width="130">
                <template #default="scope">{{ getLeadTypeLabel(scope.row.leadType) }}</template>
              </el-table-column>
              <el-table-column prop="intentLevel" width="110" sortable="custom">
                <template #header>
                  <el-tooltip :content="intentLevelTip" placement="top">
                    <span class="table-header-tip">意向等级</span>
                  </el-tooltip>
                </template>
                <template #default="scope">
                  <el-tooltip :content="`内部评分：${scope.row.productRelevanceScore ?? '-'}；${getIntentLevelDesc(scope.row.productRelevanceScore)}`" placement="top">
                    <el-tag :type="getIntentLevelTagType(scope.row.productRelevanceScore)">
                      {{ getIntentLevelLabel(scope.row.productRelevanceScore) }}
                    </el-tag>
                  </el-tooltip>
                </template>
              </el-table-column>
              <el-table-column v-if="!isPostLeadOrGroupPostAgent(detailAgent?.agentType) && !isCommentLeadAgent(detailAgent?.agentType)" label="联系方式" width="200">
                <template #default="scope">
                  {{ scope.row.whatsapp || scope.row.email || scope.row.phonenumber || scope.row.postAuthorId || '-' }}
                </template>
              </el-table-column>
              <el-table-column v-if="!isPostLeadOrGroupPostAgent(detailAgent?.agentType) && !isCommentLeadAgent(detailAgent?.agentType)" label="最近活跃" width="160">
                <template #default="scope">{{ formatDateTime(scope.row.lastPostTime || scope.row.postCreateTime) }}</template>
              </el-table-column>
              <el-table-column v-if="isPostLeadOrGroupPostAgent(detailAgent?.agentType) || isCommentLeadAgent(detailAgent?.agentType)" label="帖子链接" min-width="220">
                <template #default="scope">
                  <el-link
                    v-if="scope.row.sourcePostUrl || scope.row.postUrl || scope.row.url"
                    :href="scope.row.sourcePostUrl || scope.row.postUrl || scope.row.url"
                    target="_blank"
                    type="primary"
                  >
                    {{ scope.row.sourcePostUrl || scope.row.postUrl || scope.row.url }}
                  </el-link>
                  <span v-else>-</span>
                </template>
              </el-table-column>
              <el-table-column v-if="isPostLeadOrGroupPostAgent(detailAgent?.agentType) || isCommentLeadAgent(detailAgent?.agentType)" label="帖子内容" prop="postContent" min-width="320" />
              <el-table-column v-if="isPostLeadOrGroupPostAgent(detailAgent?.agentType)" label="帖子创建时间" prop="postCreateTime" width="160">
                <template #default="scope">{{ formatDateTime(scope.row.postCreateTime) }}</template>
              </el-table-column>
              <el-table-column v-if="isCommentLeadAgent(detailAgent?.agentType)" label="评论内容" prop="commentContent" min-width="260" />
              <el-table-column label="采集时间" prop="createTime" width="160" sortable="custom">
                <template #default="scope">{{ formatDateTime(scope.row.createTime) }}</template>
              </el-table-column>
              <!-- <el-table-column v-if="!isPostLeadOrGroupPostAgent(detailAgent?.agentType)" label="来源帖子" min-width="180">
                <template #default="scope">
                  <el-link v-if="scope.row.sourcePostUrl" :href="scope.row.sourcePostUrl" target="_blank" type="primary">
                    查看帖子
                  </el-link>
                  <span v-else>-</span>
                </template>
              </el-table-column> -->
              <el-table-column label="是否达标" width="100">
                <template #default="scope">
                  <el-tag :type="isLeadQualified(scope.row) ? 'success' : 'info'">
                    {{ isLeadQualified(scope.row) ? '达标' : '未达标' }}
                  </el-tag>
                </template>
              </el-table-column>
              <el-table-column label="AI分析原因" prop="intentReason" min-width="220" />
            </el-table>
            <Pagination
              :total="leadTotal"
              v-model:page="leadPage.pageNo"
              v-model:limit="leadPage.pageSize"
              @pagination="loadLeadPage"
            />
          </el-tab-pane>

          <el-tab-pane label="触达记录" name="touches">
            <el-table v-loading="touchLoading" :data="touchList" :show-overflow-tooltip="true">
              <el-table-column label="客户" prop="targetUrl" min-width="220" />
              <el-table-column label="方式" width="90">
                <template #default="scope">{{ scope.row.touchType === 'dm' ? '私信' : '评论' }}</template>
              </el-table-column>
              <el-table-column label="触达账号" prop="fbAccount" width="140" />
              <el-table-column label="状态" width="100">
                <template #default="scope">
                  <el-tag :type="getTouchStatusTagType(scope.row.status)">
                    {{ getTouchStatusLabel(scope.row.status) }}
                  </el-tag>
                </template>
              </el-table-column>
              <el-table-column label="发送内容" prop="generatedContent" min-width="240" />
              <el-table-column label="时间" width="160">
                <template #default="scope">{{ formatTouchTime(scope.row) }}</template>
              </el-table-column>
            </el-table>
            <Pagination
              :total="touchTotal"
              v-model:page="touchPage.pageNo"
              v-model:limit="touchPage.pageSize"
              @pagination="loadTouchPage"
            />
          </el-tab-pane>

          <el-tab-pane label="运行日志" name="logs">
            <div v-loading="runLogLoading" class="timeline-box">
              <div
                v-for="(item, index) in runLogList"
                :key="item.id"
                class="timeline-entry"
              >
                <div v-if="isRunLogDayStart(item, index)" class="timeline-day">
                  {{ formatRunLogDay(item) }}
                </div>
                <div class="timeline-item" :class="`log-${item.logLevel || 'info'}`">
                  <span class="timeline-time">{{ formatRunLogTime(item) }}</span>
                  <span class="timeline-text">{{ formatRunLogLine(item) }}</span>
                </div>
              </div>
            </div>
            <Pagination
              :total="runLogTotal"
              v-model:page="runLogPage.pageNo"
              v-model:limit="runLogPage.pageSize"
              @pagination="loadRunLogPage"
            />
          </el-tab-pane>
        </el-tabs>
      </template>
    </el-drawer>
    <GroupSelector v-model="groupSelectorVisible" @confirm="handleGroupConfirm" />
    <UserSelector v-model="userSelectorVisible" @confirm="handleCompetitorPageConfirm" />
  </div>
</template>

<script setup lang="ts" name="FbAiAgent">
defineOptions({ name: 'FbAiAgent' })
import { formatDate } from '@/utils/formatTime'
import { buildSortingField } from '@/utils'
import ContentWrap from '@/components/ContentWrap/src/ContentWrap.vue'
import { useMessage } from '@/hooks/web/useMessage'
import { FbAccountApi, filterSelectableFbAccounts, type FbAccount } from '@/api/facebook/account'
import FbAccountSelector from '../components/FbAccountSelector.vue'
import {
  FbAiAgentApi,
  type FbAiAgentConfig,
  type FbAiAgentDiscoveryLog,
  type FbAiAgentRunLog,
  type FbAiTouchRecord
} from '@/api/facebook/aiagent'
import {
  claimAndStartPendingAiAgentDetails
} from '@/utils/wpfAiAgentTaskPoller'
import GroupSelector from '../collect/components/GroupSelector.vue'
import UserSelector from '../collect/components/UserSelector.vue'
import type { FbCollectGroup } from '@/api/facebook/fbcollectgroup'
import type { FbCollectUser } from '@/api/facebook/collectuser'

const message = useMessage()

const loading = ref(false)
const saving = ref(false)
const dispatching = ref(false)
const generatingKeywords = ref(false)
const wizardVisible = ref(false)
const wizardTitle = ref('创建AI主页获客Agent')
const wizardStep = ref(0)
const wizardFormRef = ref()
const detailVisible = ref(false)
const detailTab = ref('discovery')
const groupSelectorVisible = ref(false)
const userSelectorVisible = ref(false)

const discoveryLoading = ref(false)
const leadLoading = ref(false)
const touchLoading = ref(false)
const runLogLoading = ref(false)

const list = ref<FbAiAgentConfig[]>([])
const total = ref(0)
const accountList = ref<FbAccount[]>([])
const detailAgent = ref<FbAiAgentConfig>()
const selectedAgentIds = ref<Array<string | number>>([])
const discoveryList = ref<FbAiAgentDiscoveryLog[]>([])
const leadList = ref<any[]>([])
const touchList = ref<FbAiTouchRecord[]>([])
const runLogList = ref<FbAiAgentRunLog[]>([])
const discoveryTotal = ref(0)
const leadTotal = ref(0)
const touchTotal = ref(0)
const runLogTotal = ref(0)
const discoveryPage = reactive({ pageNo: 1, pageSize: 10 })
const leadPage = reactive({ pageNo: 1, pageSize: 10, sortingFields: [] as any[] })
const touchPage = reactive({ pageNo: 1, pageSize: 10 })
const runLogPage = reactive({ pageNo: 1, pageSize: 10 })

const agentEntries = [
  {
    type: 'group_post',
    title: 'AI群帖获客',
    icon: 'ep:chat-line-round',
    description: '监控群帖发现潜在买家',
    disabled: false
  },
  {
    type: 'post_lead',
    title: 'AI帖子获客',
    icon: 'ep:document',
    description: '首页搜索结果帖子中识别潜在买家',
    disabled: false
  },
   {
    type: 'page_lead',
    title: 'AI公共主页获客',
    icon: 'ep:office-building',
    description: '首页搜索发现公共主页客户，深度采集后自动筛选和触达',
    disabled: false
  },
  {
    type: 'group_comment',
    title: 'AI群帖评论截流',
    icon: 'ep:comment',
    description: '围绕群帖评论区高意向用户做自动截流',
    disabled: false
  },
  {
    type: 'competitor_buyer',
    title: 'AI竞品监控',
    icon: 'ep:trend-charts',
    description: '从竞品买家主页帖子的评论中识别潜客',
    disabled: false
  }
]

const queryParams = reactive({
  pageNo: 1,
  pageSize: 10,
  agentName: '',
  agentType: undefined as string | undefined,
  status: undefined as number | undefined
})

const wizardForm = reactive<FbAiAgentConfig>({
  id: undefined,
  agentName: '',
  agentType: 'page_lead',
  searchMode: 'keyword',
  searchUrlTemplate: '',
  exportProduct: '',
  accountIds: '',
  accountSelectionMode: 'AUTO' as 'AUTO' | 'MANUAL',
  seedKeywords: '[]',
  keywordPool: '[]',
  keywordCursor: 0,
  keywordsPerRun: 5,
  aiKeywordExpandEnabled: true,
  aiKeywordExpandCount: 20,
  targetCustomerCount: 1000,
  executeFrequency: '1',
  executeTime: '09:00',
  targetCountries: '[]',
  targetLanguages: '["English"]',
  autoCommentEnabled: false,
  autoDmEnabled: true,
  dailyCommentLimit: 50,
  dailyDmLimit: 30,
  touchScoreThreshold: 95,
  replyDelayRange: '[1,30]',
  personaType: 'professional_sales',
  personaConfig: '',
  status: 0
})

const wizardState = reactive({
  accountIdList: [] as string[],
  accountSelectionMode: 'AUTO' as 'AUTO' | 'MANUAL',
  targetCountryList: [] as string[],
  targetLanguage: 'English',
  seedKeywordsText: '',
  keywordPoolList: [] as string[],
  newKeyword: '',
  replyDelayMin: 1,
  replyDelayMax: 30,
  groupPostSourceMode: 'select' as 'manual' | 'select',
  manualGroupUrlsText: '',
  selectedGroups: [] as FbCollectGroup[],
  competitorSourceMode: 'select' as 'manual' | 'select',
  manualCompetitorPageUrlsText: '',
  selectedCompetitorPages: [] as FbCollectUser[],
  recentDays: 1,
  latestPosts: true
})

const recentDaysCustomized = ref(false)

watch(
  () => wizardState.accountSelectionMode,
  () => {
    wizardFormRef.value?.clearValidate?.('accountIds')
  }
)

watch(
  () => wizardForm.executeFrequency,
  (value) => {
    if (recentDaysCustomized.value) return
    const days = Number(value) || 1
    wizardState.recentDays = days
  }
)

const intervalDayOptions = [1, 2, 3, 4, 5, 6, 7]

const collectionCycleDays = computed(() => Number(wizardForm.executeFrequency) || 1)

const wizardRules = {
  agentName: [{ required: true, message: '请输入Agent名称', trigger: 'blur' }],
  accountIds: [{ required: true, message: '请选择账号池', trigger: 'change' }]
}

const intentLevelOptions = [
  { level: 'A', value: 95, thresholdLabel: 'A及以上（高意向）', desc: '高意向，建议立即联系' },
  { level: 'B', value: 85, thresholdLabel: 'B及以上（推荐联系）', desc: '推荐联系' },
  { level: 'C', value: 70, thresholdLabel: 'C及以上（普通线索）', desc: '普通线索，可关注' },
  { level: 'D', value: 50, thresholdLabel: 'D及以上（全部线索）', desc: '无价值，不建议联系' }
]

const personaOptions = [
  {
    label: '专业外贸销售（推荐）',
    value: 'professional_sales',
    description: '专业、清晰地介绍产品与合作价值，适合大多数外贸获客场景。'
  },
  {
    label: '顾问式销售',
    value: 'consultant_sales',
    description: '先了解客户需求，再提供建议和方案，沟通更克制、信任感更强。'
  },
  {
    label: '朋友式开发',
    value: 'friendly_sales',
    description: '语气自然友好，先建立轻松对话，适合首次接触和社媒互动。'
  },
  {
    label: '强成交型销售',
    value: 'closer_sales',
    description: '突出优势、行动建议和时效性，适合高意向客户的明确推进。'
  }
]

const selectedPersonaOption = computed(() =>
  personaOptions.find((item) => item.value === wizardForm.personaType) || personaOptions[0]
)

const intentLevelTip = 'AI返回A/B/C/D，A：高意向，建议立即联系;B：推荐联系;C：普通线索，可关注;D：无价值，不建议联系。触达等级选B，表示触达A+B；选C，表示触达A+B+C。'

const parseJsonArray = <T,>(value?: string, fallback: T[] = []) => {
  if (!value) return fallback
  try {
    const parsed = JSON.parse(value)
    return Array.isArray(parsed) ? parsed : fallback
  } catch {
    return fallback
  }
}

const parseLines = (value: string) =>
  value
    .split('\n')
    .map((item) => item.trim())
    .filter(Boolean)

const isGroupAgent = (type?: string) => ['group_post', 'group_comment'].includes(type || '')
const isCompetitorAgent = (type?: string) => type === 'competitor_buyer'
const isPostLeadAgent = (type?: string) => type === 'post_lead'
const isPostLeadOrGroupPostAgent = (type?: string) => ['post_lead', 'group_post'].includes(type || '')
// 竞品监控与群帖评论截流都以评论用户作为最终线索。
const isCommentLeadAgent = (type?: string) => ['group_comment', 'competitor_buyer'].includes(type || '')
const isSourceUrlAgent = (type?: string) => isGroupAgent(type) || isCompetitorAgent(type)
const isHideKeywordColumn = (type?: string) => ['competitor_buyer', 'group_comment', 'group_post'].includes(type || '')

const getAgentTypeLabel = (type?: string) => {
  const map: Record<string, string> = {
    page_lead: 'AI公共主页获客',
    post_lead: 'AI帖子获客',
    group_post: 'AI群帖获客',
    group_comment: 'AI群帖评论截流',
    competitor_buyer: 'AI竞品监控'
  }
  return type ? map[type] || type : '-'
}

const getDiscoverySourceLabel = (sourceType?: string) => {
  const map: Record<string, string> = {
    page: '主页',
    post_lead: '帖子获客',
    deep: '深度采集',
    group_post: '群帖',
    group_comment_post: '评论截流帖子',
    group_comment: '评论截流评论',
    competitor_post: '竞品帖子',
    competitor_comment: '竞品评论'
  }
  return sourceType ? map[sourceType] || sourceType : '-'
}

const getGroupPostConfig = (config?: FbAiAgentConfig) => {
  if (!config?.personaConfig) return {} as any
  try {
    return JSON.parse(config.personaConfig)?.groupPostConfig || {}
  } catch {
    return {} as any
  }
}

const getPostLeadConfig = (config?: FbAiAgentConfig) => {
  if (!config?.personaConfig) return {} as any
  try {
    return JSON.parse(config.personaConfig)?.postLeadConfig || {}
  } catch {
    return {} as any
  }
}

const getCompetitorConfig = (config?: FbAiAgentConfig) => {
  if (!config?.personaConfig) return {} as any
  try {
    return JSON.parse(config.personaConfig)?.competitorConfig || {}
  } catch {
    return {} as any
  }
}

const getGroupPostUrls = (config?: FbAiAgentConfig) => {
  const groupConfig = getGroupPostConfig(config)
  if (Array.isArray(groupConfig.manualGroupUrls)) {
    return groupConfig.manualGroupUrls.filter(Boolean)
  }
  return (config?.monitorGroupIds || '').split(',').map((item) => item.trim()).filter(Boolean)
}

const getCompetitorPageUrls = (config?: FbAiAgentConfig) => {
  const competitorConfig = getCompetitorConfig(config)
  if (Array.isArray(competitorConfig.manualPageUrls)) {
    return competitorConfig.manualPageUrls.filter(Boolean)
  }
  return (config?.monitorGroupIds || '').split(',').map((item) => item.trim()).filter(Boolean)
}

const getMonitorUrls = (config?: FbAiAgentConfig) =>
  isCompetitorAgent(config?.agentType) ? getCompetitorPageUrls(config) : getGroupPostUrls(config)

const formatDateTime = (value?: string | Date) => {
  if (!value) return '-'
  return formatDate(value instanceof Date ? value : new Date(value))
}

const formatRunLogLine = (item: FbAiAgentRunLog) => {
  const title = item.title || '-'
  return item.content ? `${title}：${item.content}` : title
}

const formatRunLogTime = (item: FbAiAgentRunLog) => {
  return item.createTime ? formatDate(new Date(item.createTime), 'HH:mm:ss') : '-'
}

const getRunLogDate = (item: FbAiAgentRunLog) => {
  if (!item.createTime) return null
  const date = new Date(item.createTime)
  return Number.isNaN(date.getTime()) ? null : date
}

const formatRunLogDay = (item: FbAiAgentRunLog) => {
  const date = getRunLogDate(item)
  if (!date) return '未知日期'

  const today = new Date()
  const startOfToday = new Date(today.getFullYear(), today.getMonth(), today.getDate()).getTime()
  const startOfDate = new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime()
  const dayOffset = Math.floor((startOfToday - startOfDate) / 86400000)
  if (dayOffset === 0) return '今天'
  if (dayOffset === 1) return '昨天'
  return formatDate(date, 'YYYY年MM月DD日')
}

const isRunLogDayStart = (item: FbAiAgentRunLog, index: number) => {
  if (index === 0) return true
  const currentDate = getRunLogDate(item)
  const previousDate = getRunLogDate(runLogList.value[index - 1])
  if (!currentDate || !previousDate) return currentDate !== previousDate
  return currentDate.toDateString() !== previousDate.toDateString()
}

const formatTouchTime = (item: FbAiTouchRecord) => {
  if (item.sentTime) {
    return `发送 ${formatDateTime(item.sentTime as string)}`
  }
  if (item.scheduledTime) {
    return `计划 ${formatDateTime(item.scheduledTime as string)}`
  }
  return formatDateTime(item.createTime as string)
}

const getStatusLabel = (status?: number) => {
  const map: Record<number, string> = { 0: '草稿', 1: '运行中', 2: '暂停', 3: '停止' }
  return status !== undefined ? map[status] || String(status) : '-'
}

const getStatusTagType = (status?: number) => {
  const map: Record<number, 'info' | 'success' | 'warning' | 'danger'> = {
    0: 'info',
    1: 'success',
    2: 'warning',
    3: 'danger'
  }
  return status !== undefined ? map[status] || 'info' : 'info'
}

const getTouchStatusLabel = (status?: number) => {
  const map: Record<number, string> = { 0: '待发送', 1: '发送中', 2: '成功', 3: '失败', 4: '跳过' }
  return status !== undefined ? map[status] || String(status) : '-'
}

const getTouchStatusTagType = (status?: number) => {
  const map: Record<number, 'info' | 'success' | 'warning' | 'danger'> = {
    0: 'info',
    1: 'warning',
    2: 'success',
    3: 'danger',
    4: 'info'
  }
  return status !== undefined ? map[status] || 'info' : 'info'
}

const isLeadQualified = (lead: any) => {
  const threshold = Number(detailAgent.value?.touchScoreThreshold ?? 95)
  return Number(lead.productRelevanceScore ?? 0) >= threshold
}

const getLeadTypeLabel = (value?: string) => {
  const map: Record<string, string> = {
    page_lead: '主页线索',
    post_lead: '帖子线索',
    comment_lead: '评论线索',
    competitor_comment_lead: '竞品评论线索'
  }
  return value ? map[value] || value : '-'
}

const normalizeIntentThreshold = (score?: number, defaultValue = 95) => {
  const value = Number(score ?? defaultValue)
  if (value >= 95) return 95
  if (value >= 85) return 85
  if (value >= 70) return 70
  return 50
}

const getIntentLevelOption = (score?: number) => {
  const value = normalizeIntentThreshold(score, 50)
  return intentLevelOptions.find((item) => item.value === value) || intentLevelOptions[1]
}

const getIntentLevelLabel = (score?: number) => {
  return getIntentLevelOption(score).level
}

const getIntentLevelDesc = (score?: number) => {
  const option = getIntentLevelOption(score)
  return `${option.level}：${option.desc}`
}

const getIntentLevelTagType = (score?: number) => {
  const level = getIntentLevelLabel(score)
  const map: Record<string, 'danger' | 'warning' | 'success' | 'info'> = {
    A: 'danger',
    B: 'warning',
    C: 'success',
    D: 'info'
  }
  return map[level] || 'info'
}

const syncWizard = (config?: FbAiAgentConfig) => {
  Object.assign(wizardForm, {
    id: undefined,
    agentName: '',
    agentType: 'page_lead',
    searchMode: 'keyword',
    searchUrlTemplate: '',
    exportProduct: '',
    accountIds: '',
    seedKeywords: '[]',
    keywordPool: '[]',
    keywordCursor: 0,
    keywordsPerRun: 5,
    aiKeywordExpandEnabled: true,
    aiKeywordExpandCount: 30,
    targetCustomerCount: 1000,
    executeFrequency: '1',
    executeTime: '09:00',
    targetCountries: '[]',
    targetLanguages: '["English"]',
    autoCommentEnabled: false,
    autoDmEnabled: true,
    dailyCommentLimit: 50,
    dailyDmLimit: 30,
    touchScoreThreshold: 95,
    replyDelayRange: '[180,600]',
    personaType: 'professional_sales',
    personaConfig: '',
    status: 0,
    ...config
  })
  if (wizardForm.executeFrequency === 'daily') {
    wizardForm.executeFrequency = '1'
  }
  if (wizardForm.agentType === 'group_comment' || wizardForm.agentType === 'competitor_buyer') {
    wizardForm.autoCommentEnabled = false
    wizardForm.autoDmEnabled = true
  }
  wizardForm.touchScoreThreshold = normalizeIntentThreshold(wizardForm.touchScoreThreshold)
  wizardState.accountIdList = (wizardForm.accountIds || '')
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean)
  wizardState.accountSelectionMode = wizardForm.accountSelectionMode || 'MANUAL'
  wizardState.targetCountryList = parseJsonArray<string>(wizardForm.targetCountries)
  wizardState.targetLanguage = parseJsonArray<string>(wizardForm.targetLanguages)[0] || 'English'
  wizardState.keywordPoolList = parseJsonArray<string>(wizardForm.keywordPool)
  wizardState.seedKeywordsText = parseJsonArray<string>(wizardForm.seedKeywords).join('\n')
  const delayRange = parseJsonArray<number>(wizardForm.replyDelayRange, [1, 30])
  // 兼容旧配置：历史值按秒保存，转换为分钟展示。
  const legacySeconds = Number(delayRange[1] || 0) > 60
  wizardState.replyDelayMin = legacySeconds ? Math.max(1, Math.ceil(Number(delayRange[0] || 60) / 60)) : (delayRange[0] ?? 1)
  wizardState.replyDelayMax = legacySeconds ? Math.max(wizardState.replyDelayMin, Math.ceil(Number(delayRange[1] || 1800) / 60)) : (delayRange[1] ?? 30)
  wizardState.newKeyword = ''
  const groupConfig = getGroupPostConfig(wizardForm)
  wizardState.groupPostSourceMode = groupConfig.sourceMode || 'select'
  const groupUrls = Array.isArray(groupConfig.manualGroupUrls)
    ? groupConfig.manualGroupUrls
    : (wizardForm.monitorGroupIds || '').split(',').map((item) => item.trim()).filter(Boolean)
  wizardState.manualGroupUrlsText = groupUrls.join('\n')
  wizardState.selectedGroups = []
  const competitorConfig = getCompetitorConfig(wizardForm)
  wizardState.competitorSourceMode = competitorConfig.sourceMode || 'select'
  const competitorUrls = Array.isArray(competitorConfig.manualPageUrls)
    ? competitorConfig.manualPageUrls
    : (wizardForm.monitorGroupIds || '').split(',').map((item) => item.trim()).filter(Boolean)
  wizardState.manualCompetitorPageUrlsText = competitorUrls.join('\n')
  wizardState.selectedCompetitorPages = []
  let configuredRecentDays = Number((isCompetitorAgent(wizardForm.agentType) ? competitorConfig.recentDays : groupConfig.recentDays) || 0)
  recentDaysCustomized.value = configuredRecentDays > 0
  wizardState.recentDays = configuredRecentDays || Number(wizardForm.executeFrequency) || 1
  const postLeadConfig = getPostLeadConfig(wizardForm)
  wizardState.latestPosts = Object.prototype.hasOwnProperty.call(postLeadConfig, 'latestPosts')
    ? Boolean(postLeadConfig.latestPosts)
    : true
}

const markRecentDaysCustomized = (value?: number) => {
  if (value != null) {
    recentDaysCustomized.value = true
  }
}

const buildSubmitData = (): FbAiAgentConfig => {
  const seedKeywords = parseLines(wizardState.seedKeywordsText)
  const groupUrls = isGroupAgent(wizardForm.agentType) ? getWizardGroupPostUrls() : []
  const competitorUrls = isCompetitorAgent(wizardForm.agentType) ? getWizardCompetitorPageUrls() : []
  const monitorUrls = isCompetitorAgent(wizardForm.agentType) ? competitorUrls : groupUrls
  const persona = wizardForm.personaConfig ? JSON.parse(wizardForm.personaConfig) : {}
  if (isGroupAgent(wizardForm.agentType)) {
    persona.groupPostConfig = {
      sourceMode: wizardState.groupPostSourceMode,
      manualGroupUrls: groupUrls,
      recentDays: wizardState.recentDays
    }
  }
  if (isCompetitorAgent(wizardForm.agentType)) {
    persona.competitorConfig = {
      sourceMode: wizardState.competitorSourceMode,
      manualPageUrls: competitorUrls,
      recentDays: wizardState.recentDays
    }
  }
  if (isPostLeadAgent(wizardForm.agentType)) {
    persona.postLeadConfig = {
      latestPosts: wizardState.latestPosts
    }
  }
  return {
    ...wizardForm,
    agentType: wizardForm.agentType || 'page_lead',
    searchMode: isSourceUrlAgent(wizardForm.agentType) ? 'link' : wizardForm.searchMode,
    accountIds: wizardState.accountIdList.join(','),
    accountSelectionMode: wizardState.accountSelectionMode,
    targetCountries: JSON.stringify(wizardState.targetCountryList),
    targetLanguages: JSON.stringify([wizardState.targetLanguage.trim() || 'English']),
    seedKeywords: JSON.stringify(seedKeywords),
    keywordPool: JSON.stringify(wizardState.keywordPoolList),
    monitorGroupIds: monitorUrls.join(','),
    touchScoreThreshold: normalizeIntentThreshold(wizardForm.touchScoreThreshold),
    replyDelayRange: JSON.stringify([wizardState.replyDelayMin, wizardState.replyDelayMax]),
    personaConfig: JSON.stringify(persona)
  }
}

const validateWizard = () => {
  const seedKeywords = parseLines(wizardState.seedKeywordsText)
  if (wizardState.accountSelectionMode === 'MANUAL' && !wizardState.accountIdList.length) {
    message.warning('请选择账号池')
    return false
  }
  if (!wizardForm.exportProduct?.trim()) {
    message.warning('请输入主营/出口产品说明')
    return false
  }
  if (!isSourceUrlAgent(wizardForm.agentType) && wizardForm.searchMode === 'link') {
    const searchUrl = wizardForm.searchUrlTemplate?.trim() || ''
    try {
      const parsed = new URL(searchUrl)
      const hostname = parsed.hostname.toLowerCase()
      if (
        !/^https?:$/.test(parsed.protocol) ||
        !(hostname === 'facebook.com' || hostname.endsWith('.facebook.com')) ||
        !parsed.searchParams.has('q')
      ) {
        throw new Error('invalid')
      }
    } catch {
      message.warning('请填写包含 q 参数的 Facebook 搜索结果链接')
      return false
    }
  }
  if (isSourceUrlAgent(wizardForm.agentType)) {
    const monitorUrls = isCompetitorAgent(wizardForm.agentType) ? getWizardCompetitorPageUrls() : getWizardGroupPostUrls()
    if (!monitorUrls.length) {
      message.warning(isCompetitorAgent(wizardForm.agentType) ? '请配置竞品主页' : '请配置监控群组')
      return false
    }
    if (!wizardForm.executeTime) {
      message.warning('请选择每天执行时间')
      return false
    }
    if (wizardState.recentDays > collectionCycleDays.value) {
      message.warning(`当前执行周期为${collectionCycleDays.value}天，采集最近${wizardState.recentDays}天可能包含重复内容`)
    }
    if (wizardState.replyDelayMax < wizardState.replyDelayMin) {
      message.warning('随机间隔结束值不能小于开始值')
      return false
    }
    return true
  }
  if (!seedKeywords.length) {
    message.warning('请至少输入一个种子关键词')
    return false
  }
  if (!wizardState.keywordPoolList.length) {
    message.warning('关键词池不能为空')
    return false
  }
  if (!wizardForm.executeTime) {
    message.warning('请选择每天执行时间')
    return false
  }
  if ((wizardForm.keywordsPerRun || 1) > wizardState.keywordPoolList.length) {
    message.warning('每轮执行关键词数量不能大于关键词池总数')
    return false
  }
  if (wizardState.replyDelayMax < wizardState.replyDelayMin) {
    message.warning('随机间隔结束值不能小于开始值')
    return false
  }
  return true
}

const getWizardGroupPostUrls = () => {
  const selectedUrls = wizardState.selectedGroups.map((group) => group.url).filter(Boolean)
  const manualUrls = parseLines(wizardState.manualGroupUrlsText)
  return wizardState.groupPostSourceMode === 'select' && selectedUrls.length ? selectedUrls : manualUrls
}

const getWizardCompetitorPageUrls = () => {
  const selectedUrls = wizardState.selectedCompetitorPages.map((user) => user.url).filter(Boolean)
  const manualUrls = parseLines(wizardState.manualCompetitorPageUrlsText)
  return wizardState.competitorSourceMode === 'select' && selectedUrls.length ? selectedUrls : manualUrls
}

const validateGroupPostSourceStep = () => {
  if (!wizardForm.exportProduct?.trim()) {
    message.warning('请输入主营/出口产品说明')
    return false
  }
  if (wizardState.accountSelectionMode === 'MANUAL' && !wizardState.accountIdList.length) {
    message.warning('请选择账号池')
    return false
  }
  if (isGroupAgent(wizardForm.agentType) && !getWizardGroupPostUrls().length) {
    message.warning('请配置监控群组')
    return false
  }
  if (isCompetitorAgent(wizardForm.agentType) && !getWizardCompetitorPageUrls().length) {
    message.warning('请配置竞品主页')
    return false
  }
  return true
}

const getBaseOptions = async () => {
  const accountData = await FbAccountApi.getFbAccountPage({ pageNo: 1, pageSize: 2000 })
  accountList.value = filterSelectableFbAccounts(accountData?.list || [])
}

const getList = async () => {
  loading.value = true
  try {
    const data = await FbAiAgentApi.getConfigPage(queryParams)
    list.value = data.list || []
    total.value = data.total || 0
    const currentIds = new Set(list.value.map((item) => item.id).filter(Boolean).map(String))
    selectedAgentIds.value = selectedAgentIds.value.filter((id) => currentIds.has(String(id)))
  } finally {
    loading.value = false
  }
}

const handleQuery = () => {
  queryParams.pageNo = 1
  getList()
}

const handleSelectionChange = (rows: FbAiAgentConfig[]) => {
  selectedAgentIds.value = rows.map((row) => row.id).filter(Boolean) as Array<string | number>
}

const openCreateWizard = async (item: any) => {
  if (item.disabled) {
    message.warning('这个入口下一版再接')
    return
  }
  await getBaseOptions()
  wizardTitle.value = `创建${getAgentTypeLabel(item.type)}Agent`
  wizardStep.value = 0
  syncWizard({ agentType: item.type, status: 0 })
  wizardVisible.value = true
}

const handleEdit = async (row: FbAiAgentConfig) => {
  await getBaseOptions()
  const data = await FbAiAgentApi.getConfigById(row.id!)
  if (!data) {
    message.error('未找到Agent配置，请刷新列表后重试')
    return
  }
  wizardTitle.value = `编辑${getAgentTypeLabel(data.agentType)}Agent`
  wizardStep.value = 0
  syncWizard(data)
  wizardVisible.value = true
}

const handleStatus = async (row: FbAiAgentConfig, status: number) => {
  await FbAiAgentApi.updateStatus({ id: row.id!, status })
  message.success('状态已更新')
  await getList()
}

const handleDelete = async (row: FbAiAgentConfig) => {
  await message.delConfirm(`确认删除 Agent「${row.agentName}」吗？`)
  await FbAiAgentApi.deleteConfig(row.id!)
  message.success('删除成功')
  await getList()
}

const handleDispatch = async () => {
  if (!selectedAgentIds.value.length) {
    message.warning('请先勾选要执行的Agent')
    return
  }
  dispatching.value = true
  try {
    const result = await FbAiAgentApi.executeNow(selectedAgentIds.value)
    result.dispatched ? message.success(result.message) : message.warning(result.message)
    const details = result.details || []
    if (details.length > 0) {
      const started = await claimAndStartPendingAiAgentDetails(details.length)
      if (started > 0) {
        message.info(`已提交 ${started} 个采集明细到WPF浏览器`)
      }
    } else {
      await claimAndStartPendingAiAgentDetails()
    }
    if (detailVisible.value) {
      await loadDetailTabs()
    }
  } finally {
    dispatching.value = false
  }
}

const nextStep = async () => {
  if (wizardStep.value === 0 && !wizardForm.agentName.trim()) {
    message.warning('请先输入Agent名称')
    return
  }
  if (wizardStep.value === 0 && !validateGroupPostSourceStep()) {
    return
  }
  if (wizardStep.value === 1 && !validateWizard()) {
    return
  }
  wizardStep.value++
}

const handleGroupConfirm = (groups: FbCollectGroup[]) => {
  wizardState.selectedGroups = groups
  wizardState.manualGroupUrlsText = groups.map((group) => group.url).filter(Boolean).join('\n')
  message.success(`已选择 ${groups.length} 个群组`)
}

const handleCompetitorPageConfirm = (users: FbCollectUser[]) => {
  wizardState.selectedCompetitorPages = users
  wizardState.manualCompetitorPageUrlsText = users.map((user) => user.url).filter(Boolean).join('\n')
  message.success(`已选择 ${users.length} 个竞品主页`)
}

const removeSelectedGroup = (groupId: number) => {
  const index = wizardState.selectedGroups.findIndex((group) => group.id === groupId)
  if (index >= 0) {
    wizardState.selectedGroups.splice(index, 1)
  }
}

const removeSelectedCompetitorPage = (userId: number | string) => {
  const index = wizardState.selectedCompetitorPages.findIndex((user) => String(user.id) === String(userId))
  if (index >= 0) {
    wizardState.selectedCompetitorPages.splice(index, 1)
  }
}

const appendKeyword = () => {
  const value = wizardState.newKeyword.trim()
  if (!value) return
  if (!wizardState.keywordPoolList.includes(value)) {
    wizardState.keywordPoolList.push(value)
  }
  wizardState.newKeyword = ''
}

const removeKeyword = (index: number) => {
  wizardState.keywordPoolList.splice(index, 1)
}

const handleGenerateKeywords = async () => {
  const seedKeywords = parseLines(wizardState.seedKeywordsText)
  if (!seedKeywords.length) {
    message.warning('请先输入种子关键词')
    return
  }
  generatingKeywords.value = true
  try {
    const data = await FbAiAgentApi.generateKeywords({
      agentType: wizardForm.agentType,
      seedKeywords,
      targetCountries: wizardState.targetCountryList,
      productDescription: wizardForm.exportProduct,
      targetLanguage: wizardState.targetLanguage.trim() || 'English',
      expandCount: wizardForm.aiKeywordExpandCount
    })
    const seedSet = new Set(seedKeywords.map((item) => item.trim().toLocaleLowerCase()))
    const generatedKeywords = (data.keywords || []).filter(
      (item) => !seedSet.has(item.trim().toLocaleLowerCase())
    )
    wizardState.keywordPoolList = Array.from(
      new Set([...wizardState.keywordPoolList, ...generatedKeywords])
    )
    message.success(`已生成 ${data.keywords?.length || 0} 个关键词`)
  } finally {
    generatingKeywords.value = false
  }
}

const submitWizard = async () => {
  if (!validateWizard()) return
  saving.value = true
  try {
    const status = wizardForm.status === 0 ? 1 : wizardForm.status
    await FbAiAgentApi.saveConfig({ ...buildSubmitData(), status })
    message.success('Agent已保存')
    wizardVisible.value = false
    await getList()
  } finally {
    saving.value = false
  }
}

const handleView = async (row: FbAiAgentConfig) => {
  detailAgent.value = await FbAiAgentApi.getConfigById(row.id!)
  if (!detailAgent.value) {
    message.error('未找到Agent配置，请刷新列表后重试')
    return
  }
  detailVisible.value = true
  detailTab.value = 'discovery'
  discoveryPage.pageNo = 1
  leadPage.pageNo = 1
  leadPage.sortingFields = []
  touchPage.pageNo = 1
  runLogPage.pageNo = 1
  await loadDetailTabs()
}

const loadDiscoveryPage = async () => {
  if (!detailAgent.value?.id) return
  discoveryLoading.value = true
  try {
    const data = await FbAiAgentApi.getDiscoveryLogPage({
      ...discoveryPage,
      agentConfigId: detailAgent.value.id
    })
    discoveryList.value = (data.list || []).filter(
      (item) => item.sourceType !== 'deep' && item.keyword !== '深度采集'
    )
    discoveryTotal.value = data.total || 0
  } finally {
    discoveryLoading.value = false
  }
}

const loadLeadPage = async () => {
  if (!detailAgent.value?.id) return
  leadLoading.value = true
  try {
    const data = await FbAiAgentApi.getLeadPage({
      ...leadPage,
      agentConfigId: detailAgent.value.id
    })
    leadList.value = data.list || []
    leadTotal.value = data.total || 0
  } finally {
    leadLoading.value = false
  }
}

const loadTouchPage = async () => {
  if (!detailAgent.value?.id) return
  touchLoading.value = true
  try {
    const data = await FbAiAgentApi.getTouchRecordPage({
      ...touchPage,
      agentConfigId: detailAgent.value.id
    })
    touchList.value = data.list || []
    touchTotal.value = data.total || 0
  } finally {
    touchLoading.value = false
  }
}

const handleLeadSortChange = (params: any) => {
  leadPage.pageNo = 1
  leadPage.sortingFields = params.order ? [buildSortingField(params)] : []
  loadLeadPage()
}

const loadRunLogPage = async () => {
  if (!detailAgent.value?.id) return
  runLogLoading.value = true
  try {
    const data = await FbAiAgentApi.getRunLogPage({
      ...runLogPage,
      agentConfigId: detailAgent.value.id
    })
    runLogList.value = data.list || []
    runLogTotal.value = data.total || 0
  } finally {
    runLogLoading.value = false
  }
}

const loadDetailTabs = async () => {
  if (!detailAgent.value?.id) return
  const agentConfigId = detailAgent.value.id
  discoveryLoading.value = true
  leadLoading.value = true
  touchLoading.value = true
  runLogLoading.value = true
  try {
    const [discoveryData, leadData, touchData, runLogData] = await Promise.all([
      FbAiAgentApi.getDiscoveryLogPage({ ...discoveryPage, agentConfigId }),
      FbAiAgentApi.getLeadPage({ ...leadPage, agentConfigId }),
      FbAiAgentApi.getTouchRecordPage({ ...touchPage, agentConfigId }),
      FbAiAgentApi.getRunLogPage({ ...runLogPage, agentConfigId })
    ])
    discoveryList.value = (discoveryData.list || []).filter(
      (item) => item.sourceType !== 'deep' && item.keyword !== '深度采集'
    )
    discoveryTotal.value = discoveryData.total || 0
    leadList.value = leadData.list || []
    leadTotal.value = leadData.total || 0
    touchList.value = touchData.list || []
    touchTotal.value = touchData.total || 0
    runLogList.value = runLogData.list || []
    runLogTotal.value = runLogData.total || 0
  } finally {
    discoveryLoading.value = false
    leadLoading.value = false
    touchLoading.value = false
    runLogLoading.value = false
  }
}

const handleAiAgentCollectSaved = async () => {
  await getList()
  if (detailVisible.value && detailAgent.value?.id) {
    await loadDetailTabs()
  }
}

onMounted(async () => {
  await getBaseOptions()
  await getList()
  window.addEventListener('fb:ai-agent:collect:saved', handleAiAgentCollectSaved)
})

onBeforeUnmount(() => {
  window.removeEventListener('fb:ai-agent:collect:saved', handleAiAgentCollectSaved)
})
</script>

<style scoped lang="scss">
.ai-agent-page {
  .panel-title {
    font-size: 16px;
    font-weight: 600;
    color: var(--el-text-color-primary);
  }

  .panel-subtitle {
    margin-top: 4px;
    margin-bottom: 16px;
    font-size: 12px;
    color: var(--el-text-color-secondary);
  }

  .agent-entry-list {
    display: grid;
    gap: 12px;
  }

  .agent-entry {
    padding: 14px;
    border: 1px solid var(--el-border-color);
    border-radius: 8px;
    cursor: pointer;
    transition: 0.2s ease;
    background: linear-gradient(135deg, #ffffff 0%, #f5f7fa 100%);
  }

  .agent-entry:hover {
    border-color: var(--el-color-primary);
    transform: translateY(-1px);
  }

  .agent-entry.disabled {
    opacity: 0.65;
    cursor: not-allowed;
  }

  .agent-entry__header {
    display: flex;
    align-items: center;
    gap: 8px;
    font-size: 14px;
    font-weight: 600;
    margin-bottom: 8px;
  }

  .agent-entry__desc {
    min-height: 38px;
    margin-bottom: 10px;
    font-size: 12px;
    color: var(--el-text-color-secondary);
    line-height: 1.5;
  }

  .list-header,
  .detail-head {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    gap: 12px;
    margin-bottom: 16px;
  }

  .inline-row {
    display: flex;
    align-items: center;
    gap: 12px;
    flex-wrap: wrap;
  }

  .form-tip {
    margin-top: 4px;
    color: var(--el-text-color-secondary);
    font-size: 12px;
    line-height: 1.5;
  }

  .persona-option {
    display: grid;
    gap: 2px;
    padding: 4px 0;
    line-height: 18px;
  }

  .persona-option span:last-child {
    color: var(--el-text-color-secondary);
    font-size: 12px;
    white-space: normal;
  }

  :global(.persona-select-dropdown .el-select-dropdown__item) {
    height: auto;
    min-height: 48px;
    line-height: normal;
    padding-top: 4px;
    padding-bottom: 4px;
  }

  .form-label-tip,
  .table-header-tip {
    cursor: help;
    border-bottom: 1px dashed var(--el-text-color-placeholder);
  }

  .keyword-pool {
    width: 100%;
    height: 140px;
    box-sizing: border-box;
    overflow-y: auto;
    padding: 10px;
    border: 1px dashed var(--el-border-color);
    border-radius: 8px;
    display: flex;
    flex-wrap: wrap;
    align-content: flex-start;
    gap: 8px;
    background: #fafafa;
  }

  .keyword-tag {
    margin-right: 0;
  }

  .detail-title {
    font-size: 18px;
    font-weight: 600;
    color: var(--el-text-color-primary);
  }

  .timeline-box {
    display: grid;
    gap: 8px;
    padding-right: 8px;
  }

  .timeline-entry {
    display: grid;
    gap: 6px;
  }

  .timeline-day {
    padding: 8px 2px 2px;
    color: var(--el-text-color-primary);
    font-size: 13px;
    font-weight: 600;
  }

  .timeline-item {
    display: grid;
    grid-template-columns: 160px minmax(0, 1fr);
    column-gap: 12px;
    align-items: center;
    min-height: 34px;
    padding: 7px 10px;
    border-left: 3px solid var(--el-color-primary);
    border-radius: 4px;
    background: #fafafa;
    color: var(--el-text-color-primary);
  }

  .timeline-item.log-success {
    border-left-color: var(--el-color-success);
  }

  .timeline-item.log-warning {
    border-left-color: var(--el-color-warning);
    background: var(--el-color-warning-light-9);
  }

  .timeline-item.log-danger,
  .timeline-item.log-error {
    border-left-color: var(--el-color-danger);
    background: var(--el-color-danger-light-9);
  }

  .timeline-time {
    font-size: 12px;
    color: var(--el-text-color-secondary);
    white-space: nowrap;
  }

  .timeline-text {
    width: 100%;
    font-size: 13px;
    line-height: 20px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
}
</style>
