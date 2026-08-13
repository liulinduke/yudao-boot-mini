<script lang="ts" setup>
import { ElMessageBox } from 'element-plus'

import avatarImg from '@/assets/imgs/avatar.jpg'
import { useDesign } from '@/hooks/web/useDesign'
import { useTagsViewStore } from '@/store/modules/tagsView'
import { useUserStore } from '@/store/modules/user'
import TenantVisit from '@/layout/components/TenantVisit/index.vue'
import { checkPermi } from '@/utils/permission'

defineOptions({ name: 'UserInfo' })

const props = defineProps({
  placement: {
    type: String,
    default: 'bottom-end'
  },
  showName: {
    type: Boolean,
    default: true
  },
  compact: {
    type: Boolean,
    default: false
  },
  hoverable: {
    type: Boolean,
    default: true
  }
})

const { t } = useI18n()

const { push, replace } = useRouter()

const userStore = useUserStore()

const tagsViewStore = useTagsViewStore()

const { getPrefixCls } = useDesign()

const prefixCls = getPrefixCls('user-info')

const avatar = computed(() => userStore.user.avatar || avatarImg)
const userName = computed(() => userStore.user.nickname ?? 'Admin')
const hasTenantVisitPermission = computed(
  () => import.meta.env.VITE_APP_TENANT_ENABLE === 'true' && checkPermi(['system:tenant:visit'])
)

const loginOut = async () => {
  try {
    await ElMessageBox.confirm(t('common.loginOutMessage'), t('common.reminder'), {
      confirmButtonText: t('common.ok'),
      cancelButtonText: t('common.cancel'),
      type: 'warning'
    })
    await userStore.loginOut()
    tagsViewStore.delAllViews()
    replace('/login?redirect=/index')
  } catch {}
}
const toProfile = async () => {
  push('/user/profile')
}
</script>

<template>
  <ElDropdown
    :class="[prefixCls, { 'custom-hover': props.hoverable }]"
    trigger="click"
    :placement="props.placement"
  >
    <div class="flex items-center">
      <ElAvatar
        :src="avatar"
        alt=""
        :class="[
          'rounded-[50%]',
          props.compact ? '!w-28px !h-28px' : 'w-[calc(var(--logo-height)-25px)]'
        ]"
      />
      <span
        v-if="props.showName"
        class="pl-[5px] text-14px text-[var(--top-header-text-color)] <lg:hidden"
      >
        {{ userName }}
      </span>
    </div>
    <template #dropdown>
      <ElDropdownMenu>
        <div v-if="hasTenantVisitPermission" class="user-tenant-switch">
          <TenantVisit />
        </div>
        <ElDropdownItem>
          <Icon icon="ep:tools" />
          <div @click="toProfile">{{ t('common.profile') }}</div>
        </ElDropdownItem>
        <ElDropdownItem divided @click="loginOut">
          <Icon icon="ep:switch-button" />
          <div>{{ t('common.loginOut') }}</div>
        </ElDropdownItem>
      </ElDropdownMenu>
    </template>
  </ElDropdown>
</template>

<style scoped lang="scss">
.user-tenant-switch {
  padding: 8px 12px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

:deep(.user-tenant-switch .el-select) {
  width: 180px !important;
}
</style>
