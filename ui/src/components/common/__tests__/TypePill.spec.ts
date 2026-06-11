import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import TypePill from '@/components/common/TypePill.vue'
import { sportToPillKind } from '@/components/common/pills'

describe('TypePill', () => {
  it('renders its slot content', () => {
    const wrapper = mount(TypePill, {
      props: { kind: 'run' as const },
      slots: { default: 'Run' },
    })

    expect(wrapper.text()).toBe('Run')

    wrapper.unmount()
  })
})

describe('sportToPillKind', () => {
  it('maps known sports to their pill kinds', () => {
    expect(sportToPillKind('Run')).toBe('run')
    expect(sportToPillKind('Bike')).toBe('bike')
    expect(sportToPillKind('Swim')).toBe('swim')
    expect(sportToPillKind('Strength')).toBe('strength')
    expect(sportToPillKind('Triathlon')).toBe('triathlon')
  })

  it('falls back to neutral for unknown sports', () => {
    expect(sportToPillKind('Rowing')).toBe('neutral')
  })
})
