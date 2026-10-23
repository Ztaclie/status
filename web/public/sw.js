self.addEventListener("push", (event) => {
  const data = event.data ? event.data.json() : { title: "The binding", body: "The binding is waiting." };
  event.waitUntil(self.registration.showNotification(data.title, { body: data.body }));
});
