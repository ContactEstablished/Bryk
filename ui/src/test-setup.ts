// Polyfill PointerEvent for jsdom — reka-ui / shadcn-vue components dispatch
// pointer events on mount and jsdom lacks the constructor.
if (typeof globalThis.PointerEvent === 'undefined') {
  // @ts-expect-error — minimal stub sufficient for component mount smoke tests.
  globalThis.PointerEvent = class PointerEvent extends Event {
    constructor(type: string, init?: EventInit) {
      super(type, init)
    }
  }
}

// jsdom does not implement these; reka-ui's SelectTrigger calls them.
if (typeof Element !== 'undefined') {
  if (!Element.prototype.hasPointerCapture) {
    Element.prototype.hasPointerCapture = () => false
  }
  if (!Element.prototype.setPointerCapture) {
    Element.prototype.setPointerCapture = () => {}
  }
  if (!Element.prototype.releasePointerCapture) {
    Element.prototype.releasePointerCapture = () => {}
  }
  if (!Element.prototype.scrollIntoView) {
    Element.prototype.scrollIntoView = () => {}
  }
}
